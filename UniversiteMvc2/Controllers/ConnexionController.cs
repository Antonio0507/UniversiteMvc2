using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

// Connexion, déconnexion et changement de mot de passe de l'utilisateur connecté
public class ConnexionController : AppController
{
    public const int EchecsMaximum = 5;
    public static readonly TimeSpan DureeVerrouillage = TimeSpan.FromMinutes(15);

    // Vérifié quand l'e-mail est inconnu, pour que le temps de réponse ne révèle pas si le compte existe
    private static readonly string HashFactice = new PasswordHasher<Compte>().HashPassword(new Compte(), Guid.NewGuid().ToString());

    private readonly PasswordHasher<Compte> _hacheur = new();

    public ConnexionController(ApplicationDbContext context) : base(context) { }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Index(string? retour)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewData["retour"] = retour;
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("connexion")]
    public async Task<IActionResult> Index(string? email, string? motDePasse, string? retour)
    {
        ViewData["retour"] = retour;
        ViewData["email"] = email;
        const string refus = "E-mail ou mot de passe incorrect.";

        var identifiant = email?.Trim().ToLowerInvariant() ?? "";
        var compte = await _context.Comptes.FirstOrDefaultAsync(c => c.Email == identifiant);
        var maintenant = DateTime.UtcNow;

        if (compte?.VerrouilleJusquA > maintenant)
        {
            ModelState.AddModelError(string.Empty, $"Compte temporairement verrouillé après {EchecsMaximum} échecs. Réessayez dans quelques minutes.");
            return View();
        }

        var verification = _hacheur.VerifyHashedPassword(compte ?? new Compte(), compte?.MotDePasseHash ?? HashFactice, motDePasse ?? "");

        if (compte == null || !compte.Actif || verification == PasswordVerificationResult.Failed)
        {
            if (compte != null)
            {
                compte.EchecsConnexion++;
                if (compte.EchecsConnexion >= EchecsMaximum)
                {
                    compte.VerrouilleJusquA = maintenant.Add(DureeVerrouillage);
                    compte.EchecsConnexion = 0;
                }
            }
            _context.JournalAudit.Add(new JournalAudit { Auteur = Tronquer(identifiant, 100), Action = "Connexion refusée", Entite = nameof(Compte), EntiteId = compte?.Id });
            await _context.SaveChangesAsync();

            ModelState.AddModelError(string.Empty, refus);
            return View();
        }

        compte.EchecsConnexion = 0;
        compte.VerrouilleJusquA = null;
        compte.DerniereConnexion = maintenant;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            compte.MotDePasseHash = _hacheur.HashPassword(compte, motDePasse!);
        _context.JournalAudit.Add(new JournalAudit { Auteur = compte.Email, Action = "Connexion", Entite = nameof(Compte), EntiteId = compte.Id });
        await _context.SaveChangesAsync();

        await OuvrirSessionAsync(compte);

        if (compte.DoitChangerMotDePasse) return RedirectToAction(nameof(MotDePasse));
        return !string.IsNullOrEmpty(retour) && Url.IsLocalUrl(retour) ? LocalRedirect(retour) : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deconnexion()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult MotDePasse() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MotDePasse(string? actuel, string? nouveau, string? confirmation)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var compte = await _context.Comptes.FindAsync(id);
        if (compte == null) return Forbid();

        if (_hacheur.VerifyHashedPassword(compte, compte.MotDePasseHash, actuel ?? "") == PasswordVerificationResult.Failed)
            ModelState.AddModelError("actuel", "Mot de passe actuel incorrect.");
        else if (MotsDePasse.Verifier(nouveau) is { } probleme)
            ModelState.AddModelError("nouveau", probleme);
        else if (nouveau != confirmation)
            ModelState.AddModelError("confirmation", "Les deux saisies ne correspondent pas.");
        else if (nouveau == actuel)
            ModelState.AddModelError("nouveau", "Le nouveau mot de passe doit être différent de l'actuel.");

        if (!ModelState.IsValid) return View();

        compte.MotDePasseHash = _hacheur.HashPassword(compte, nouveau!);
        compte.DoitChangerMotDePasse = false;
        compte.Jeton = Guid.NewGuid().ToString();   // déconnecte les autres sessions de ce compte
        await _context.SaveChangesAsync();

        await OuvrirSessionAsync(compte);
        Succes("Mot de passe modifié.");
        return RedirectToAction("Index", "Home");
    }

    private Task OuvrirSessionAsync(Compte compte)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, compte.Id.ToString()),
            new(ClaimTypes.Name, compte.Nom),
            new(ClaimTypes.Email, compte.Email),
            new(ClaimTypes.Role, compte.Role),
            new(Droits.ClaimJeton, compte.Jeton)
        };
        if (compte.DoitChangerMotDePasse) claims.Add(new Claim(Droits.ClaimDoitChanger, "1"));

        var identite = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identite));
    }

    private static string Tronquer(string texte, int longueur) => texte.Length <= longueur ? texte : texte[..longueur];
}
