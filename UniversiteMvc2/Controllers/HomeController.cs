using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

/// <summary>Tableau de bord et pages d'erreur.</summary>
public class HomeController : AppController
{
    /// <summary>Crée le contrôleur.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    public HomeController(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// Tableau de bord : par défaut l'année académique en cours. Un étudiant est renvoyé vers son espace.
    /// </summary>
    /// <param name="annee">Année affichée ; par défaut l'année en cours, sinon la plus récente.</param>
    public async Task<IActionResult> Index(int? annee)
    {
        if (User.IsInRole(Roles.Etudiant)) return RedirectToAction("Index", "EspaceEtudiant");

        var annees = await _context.AnneesAcad.AsNoTracking().OrderByDescending(a => a.DateDebut).ToListAsync();
        var aujourdhui = DateTime.Today;
        var choisie = annees.FirstOrDefault(a => a.Id == annee)
            ?? annees.FirstOrDefault(a => a.DateDebut <= aujourdhui && a.DateFin >= aujourdhui)
            ?? annees.FirstOrDefault();
        var anneeId = choisie?.Id ?? 0;

        var inscriptions = _context.Inscriptions.Where(i => i.AnneeAcadId == anneeId);
        var parStatut = await inscriptions.GroupBy(i => i.Statut)
            .Select(g => new { Statut = g.Key, Nombre = g.Count() })
            .ToListAsync();

        var impayes = await _context.ImpayesAsync(anneeId);

        var modele = new TableauDeBord
        {
            Annee = choisie,
            Annees = annees,
            Etudiants = await _context.Etudiants.CountAsync(),
            Enseignants = await _context.Enseignants.CountAsync(),
            Inscriptions = await inscriptions.CountAsync(),
            Encaisse = await _context.Paiements
                .Where(p => p.Inscription!.AnneeAcadId == anneeId)
                .SumAsync(p => (decimal?)p.Montant) ?? 0,
            ResteAEncaisser = impayes.Sum(l => l.Reste),
            Impayes = impayes.Count,
            ParFiliere = await _context.Filieres.OrderBy(f => f.Code)
                .Select(f => new Repartition(f.Code, f.Nom, f.Inscriptions.Count(i => i.AnneeAcadId == anneeId)))
                .ToListAsync(),
            ParStatut = Inscription.Statuts
                .Select(s => new Repartition(s, s, parStatut.FirstOrDefault(x => x.Statut == s)?.Nombre ?? 0))
                .ToList(),
            InscriptionsEnAttente = await inscriptions.CountAsync(i => i.Statut == Inscription.EnAttente),
            InscriptionsSansPaiement = await inscriptions.CountAsync(i => i.Statut == Inscription.Validee && !i.Paiements.Any()),
            CoursNonAttribues = await _context.Cours.CountAsync(c => c.EnseignantId == null),
            DernieresInscriptions = await _context.Inscriptions.AsNoTracking()
                .Include(i => i.Etudiant).Include(i => i.Filiere).Include(i => i.Niveau)
                .OrderByDescending(i => i.DateInscription).ThenByDescending(i => i.Id)
                .Take(6).ToListAsync(),
            DerniersPaiements = await _context.Paiements.AsNoTracking()
                .Include(p => p.Inscription).ThenInclude(i => i!.Etudiant)
                .OrderByDescending(p => p.DatePaiement).ThenByDescending(p => p.Id)
                .Take(6).ToListAsync()
        };
        return View(modele);
    }

    /// <summary>
    /// Page affichée pour les codes d'erreur HTTP (404, 403, 429…). Après un POST refusé, elle est rejouée en POST :
    /// le jeton anti-CSRF ne doit pas y être revérifié, sinon elle reste vide.
    /// </summary>
    /// <param name="code">Code HTTP de l'erreur.</param>
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public IActionResult Introuvable([FromQuery] int code = 404) => View(code);

    /// <summary>
    /// Page générique affichée après une panne imprévue, avec la référence de la requête à retrouver dans les logs.
    /// </summary>
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
