using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

/// <summary>
/// Gestion des comptes du personnel et des étudiants (administrateurs uniquement, voir Navigation). Les comptes se
/// désactivent mais ne se suppriment pas : le journal d'audit continue à les référencer. Les comptes étudiants sont
/// créés par l'étudiant lui-même (inscription en ligne ou activation) et gardent le profil Étudiant.
/// </summary>
public class ComptesController : AppController
{
    private readonly PasswordHasher<Compte> _hacheur = new();

    /// <summary>Crée le contrôleur.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    public ComptesController(ApplicationDbContext context) : base(context) { }

    /// <summary>Liste des comptes, avec profil, état et dernière connexion : le personnel, ou les étudiants.</summary>
    /// <param name="etudiants">true pour les comptes étudiants.</param>
    public async Task<IActionResult> Index(bool etudiants = false)
    {
        ViewData["etudiants"] = etudiants;
        return View(await _context.Comptes.AsNoTracking()
            .Include(c => c.Etudiant)
            .Where(c => etudiants ? c.Role == Roles.Etudiant : c.Role != Roles.Etudiant)
            .OrderBy(c => c.Nom)
            .ToListAsync());
    }

    /// <summary>Affiche le formulaire de création d'un compte.</summary>
    public IActionResult Create()
    {
        RemplirListes();
        return View("Formulaire", new Compte());
    }

    /// <summary>
    /// Crée un compte avec un mot de passe provisoire, affiché une seule fois pour être transmis à la personne.
    /// </summary>
    /// <param name="compte">E-mail, nom et profil saisis.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Email,Nom,Role")] Compte compte)
    {
        // Renseigné par le serveur, pas par le formulaire
        ModelState.Remove(nameof(Compte.MotDePasseHash));
        ModelState.Remove(nameof(Compte.Jeton));
        if (!Roles.Personnel.Contains(compte.Role))
            ModelState.AddModelError(nameof(Compte.Role), "Les comptes étudiants sont créés par les étudiants eux-mêmes (inscription en ligne).");

        if (ModelState.IsValid)
        {
            compte.Email = compte.Email.Trim().ToLowerInvariant();
            var provisoire = MotsDePasse.Generer();
            compte.MotDePasseHash = _hacheur.HashPassword(compte, provisoire);
            _context.Add(compte);
            if (await EnregistrerAsync())
            {
                Information($"Compte créé. Mot de passe provisoire de {compte.Email} : {provisoire} — à transmettre maintenant, il ne sera plus affiché.");
                return RedirectToAction(nameof(Index));
            }
        }
        RemplirListes(compte.Role);
        return View("Formulaire", compte);
    }

    /// <summary>Affiche le formulaire de modification d'un compte.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var compte = await _context.Comptes.FindAsync(id);
        if (compte == null) return NotFound();

        RemplirListes(compte.Role);
        return View("Formulaire", compte);
    }

    /// <summary>
    /// Enregistre le nom, le profil et l'état du compte. Un changement de droits ferme ses sessions ; le dernier
    /// administrateur actif ne peut pas être retiré.
    /// </summary>
    /// <param name="id">Identifiant de l'élément.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id)
    {
        var compte = await _context.Comptes.FindAsync(id);
        if (compte == null) return NotFound();

        var roleAvant = compte.Role;
        var actifAvant = compte.Actif;

        if (await TryUpdateModelAsync(compte, string.Empty, c => c.Nom, c => c.Role, c => c.Actif))
        {
            // Un compte étudiant reste étudiant, et un compte du personnel ne le devient pas
            if (compte.EtudiantId != null) compte.Role = Roles.Etudiant;
            else if (!Roles.Personnel.Contains(compte.Role))
                ModelState.AddModelError(nameof(Compte.Role), "Le profil Étudiant est réservé aux comptes créés par les étudiants.");

            if ((roleAvant == Roles.Administrateur && actifAvant) && (compte.Role != Roles.Administrateur || !compte.Actif)
                && !await _context.Comptes.AnyAsync(c => c.Id != id && c.Actif && c.Role == Roles.Administrateur))
                ModelState.AddModelError(string.Empty, "Il doit rester au moins un administrateur actif.");

            if (ModelState.IsValid)
            {
                // Un changement de droits s'applique tout de suite : les sessions en cours sont fermées
                if (compte.Role != roleAvant || compte.Actif != actifAvant) compte.Jeton = Guid.NewGuid().ToString();
                if (await EnregistrerAsync())
                {
                    Succes("Compte mis à jour.");
                    return RedirectToAction(nameof(Index), new { etudiants = compte.EtudiantId != null });
                }
            }
        }
        RemplirListes(compte.Role);
        return View("Formulaire", compte);
    }

    /// <summary>
    /// Réinitialise le mot de passe d'un compte (mot de passe oublié) : nouveau provisoire, compte déverrouillé,
    /// sessions fermées.
    /// </summary>
    /// <param name="id">Identifiant de l'élément.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reinitialiser(int id)
    {
        var compte = await _context.Comptes.FindAsync(id);
        if (compte == null) return NotFound();

        if (compte.Id.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            Erreur("Pour votre propre compte, utilisez « Mot de passe » dans l'en-tête.");
            return RedirectToAction(nameof(Index));
        }

        var provisoire = MotsDePasse.Reinitialiser(compte);
        await _context.SaveChangesAsync();

        Information($"Mot de passe provisoire de {compte.Email} : {provisoire} — à transmettre maintenant, il ne sera plus affiché.");
        return RedirectToAction(nameof(Index), new { etudiants = compte.EtudiantId != null });
    }

    private void RemplirListes(string? role = null) => ViewData["Role"] = new SelectList(Roles.Personnel, role);
}
