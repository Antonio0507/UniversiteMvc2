using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

/// <summary>
/// Pages publiques des étudiants : inscription en ligne d'un nouvel étudiant, et activation du compte d'un étudiant
/// déjà enregistré par la scolarité. Dans les deux cas, l'étudiant est connecté à son espace.
/// </summary>
[AllowAnonymous]
public class InscriptionEnLigneController : AppController
{
    private readonly PasswordHasher<Compte> _hacheur = new();
    private readonly ILogger<InscriptionEnLigneController> _journal;

    /// <summary>Crée le contrôleur.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    /// <param name="journal">Logs techniques des inscriptions et activations.</param>
    public InscriptionEnLigneController(ApplicationDbContext context, ILogger<InscriptionEnLigneController> journal) : base(context)
    {
        _journal = journal;
    }

    /// <summary>
    /// Affiche le formulaire d'inscription en ligne pour l'année ouverte ; un utilisateur déjà connecté va à son
    /// accueil.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        await RemplirListesAsync();
        return View(new Candidature());
    }

    /// <summary>
    /// Crée le dossier étudiant (matricule attribué), une inscription « En attente » pour l'année ouverte et le
    /// compte de connexion, puis ouvre la session. La scolarité valide ensuite l'inscription.
    /// </summary>
    /// <param name="candidature">Identité, cursus demandé et mot de passe choisi.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("connexion")]
    public async Task<IActionResult> Index(Candidature candidature)
    {
        var annee = await _context.AnneeOuverteAsync();
        if (annee == null) ModelState.AddModelError(string.Empty, "Les inscriptions en ligne ne sont pas ouvertes : aucune année académique n'est en cours.");

        var email = candidature.Email?.Trim().ToLowerInvariant() ?? "";
        VerifierNaissance(nameof(Candidature.DateNaissance), candidature.DateNaissance);
        VerifierMotDePasse(nameof(Candidature.MotDePasse), candidature.MotDePasse);
        if (!await _context.Filieres.AnyAsync(f => f.Id == candidature.FiliereId))
            ModelState.AddModelError(nameof(Candidature.FiliereId), "Choisissez une filière.");
        if (!await _context.Niveaux.AnyAsync(n => n.Id == candidature.NiveauId))
            ModelState.AddModelError(nameof(Candidature.NiveauId), "Choisissez un niveau.");
        if (await EmailPrisAsync(email))
            ModelState.AddModelError(nameof(Candidature.Email), "Un compte ou un dossier existe déjà avec cette adresse. Déjà enregistré par la scolarité ? Activez votre compte.");

        if (ModelState.IsValid)
        {
            var etudiant = new Etudiant
            {
                Nom = candidature.Nom.Trim().ToUpperInvariant(),
                Prenom = candidature.Prenom.Trim(),
                DateNaissance = candidature.DateNaissance!.Value.Date,
                Email = email,
                Telephone = string.IsNullOrWhiteSpace(candidature.Telephone) ? null : candidature.Telephone.Trim(),
                Matricule = await _context.ProchainMatriculeAsync()
            };
            _context.Add(new Inscription
            {
                Etudiant = etudiant,
                AnneeAcadId = annee!.Id,
                FiliereId = candidature.FiliereId!.Value,
                NiveauId = candidature.NiveauId!.Value,
                DateInscription = DateTime.Today,
                Statut = Inscription.EnAttente
            });
            var compte = NouveauCompte(etudiant, candidature.MotDePasse);

            if (await EnregistrerAsync())
            {
                _journal.LogInformation("Inscription en ligne de {Identifiant} ({Matricule})", compte.Email, etudiant.Matricule);
                await Sessions.OuvrirAsync(HttpContext, compte);
                Succes($"Bienvenue ! Votre matricule est {etudiant.Matricule}. Votre inscription est en attente de validation par la scolarité.");
                return RedirectToAction("Index", "EspaceEtudiant");
            }
        }

        await RemplirListesAsync(candidature);
        return View(candidature);
    }

    /// <summary>Affiche le formulaire d'activation du compte d'un étudiant déjà enregistré.</summary>
    [HttpGet]
    public IActionResult Activer()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new Activation());
    }

    /// <summary>
    /// Crée le compte d'un étudiant dont le dossier existe, si le matricule, la date de naissance et l'e-mail
    /// correspondent tous au dossier et qu'il n'a pas encore de compte ; puis ouvre la session.
    /// </summary>
    /// <param name="activation">Matricule, date de naissance, e-mail et mot de passe choisi.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("connexion")]
    public async Task<IActionResult> Activer(Activation activation)
    {
        VerifierMotDePasse(nameof(Activation.MotDePasse), activation.MotDePasse);
        if (!ModelState.IsValid) return View(activation);

        var email = activation.Email.Trim().ToLowerInvariant();
        var matricule = activation.Matricule.Trim();
        var naissance = activation.DateNaissance!.Value.Date;
        var etudiant = await _context.Etudiants.FirstOrDefaultAsync(e => e.Matricule == matricule && e.Email == email && e.DateNaissance == naissance);

        // Même message dans tous les cas : la page ne doit pas révéler quels dossiers existent
        if (etudiant == null || await _context.Comptes.AnyAsync(c => c.EtudiantId == etudiant.Id || c.Email == email))
        {
            _journal.LogWarning("Activation refusée pour {Matricule} depuis {Ip}", matricule.Length <= 20 ? matricule : matricule[..20], HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError(string.Empty, "Ces informations ne correspondent à aucun dossier sans compte. Vérifiez-les, ou adressez-vous à la scolarité.");
            return View(activation);
        }

        var compte = NouveauCompte(etudiant, activation.MotDePasse);
        if (await EnregistrerAsync())
        {
            _journal.LogInformation("Compte étudiant activé : {Identifiant} ({Matricule})", compte.Email, etudiant.Matricule);
            await Sessions.OuvrirAsync(HttpContext, compte);
            Succes("Votre compte est activé.");
            return RedirectToAction("Index", "EspaceEtudiant");
        }
        return View(activation);
    }

    // Le mot de passe est choisi par l'étudiant : pas de changement obligatoire à la première connexion
    private Compte NouveauCompte(Etudiant etudiant, string motDePasse)
    {
        var compte = new Compte
        {
            Email = etudiant.Email,
            Nom = $"{etudiant.Prenom} {etudiant.Nom}",
            Role = Roles.Etudiant,
            Etudiant = etudiant,
            DoitChangerMotDePasse = false
        };
        compte.MotDePasseHash = _hacheur.HashPassword(compte, motDePasse);
        _context.Comptes.Add(compte);
        return compte;
    }

    // Adresse déjà utilisée par un compte, un étudiant ou un enseignant
    private async Task<bool> EmailPrisAsync(string email) =>
        await _context.Comptes.AnyAsync(c => c.Email == email) || await _context.Utilisateurs.AnyAsync(u => u.Email == email);

    private void VerifierMotDePasse(string champ, string? motDePasse)
    {
        if (MotsDePasse.Verifier(motDePasse) is { } probleme) ModelState.AddModelError(champ, probleme);
    }

    private void VerifierNaissance(string champ, DateTime? naissance)
    {
        if (naissance != null && (naissance.Value.Date >= DateTime.Today.AddYears(-10) || naissance.Value.Year < 1900))
            ModelState.AddModelError(champ, "Date de naissance invalide.");
    }

    private async Task RemplirListesAsync(Candidature? candidature = null)
    {
        ViewData["annee"] = await _context.AnneeOuverteAsync();
        ViewData[nameof(Candidature.FiliereId)] = new SelectList(await _context.Filieres.OrderBy(f => f.Nom).ToListAsync(), nameof(Filiere.Id), nameof(Filiere.Nom), candidature?.FiliereId);
        ViewData[nameof(Candidature.NiveauId)] = new SelectList(await _context.Niveaux.OrderBy(n => n.Id).ToListAsync(), nameof(Niveau.Id), nameof(Niveau.Libelle), candidature?.NiveauId);
    }
}
