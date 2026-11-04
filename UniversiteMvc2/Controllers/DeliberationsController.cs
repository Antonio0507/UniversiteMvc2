using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

// Résultats de fin d'année par promotion : moyenne, crédits, rang et décision du jury, calculés à partir des notes
public class DeliberationsController : AppController
{
    public DeliberationsController(ApplicationDbContext context) : base(context) { }

    public async Task<IActionResult> Index(int? anneeId, int? filiereId, int? niveauId, string? decision)
    {
        var annee = await AnneeOuCouranteAsync(anneeId);
        ViewData["annee"] = annee;
        ViewData["AnneeId"] = new SelectList(await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).ToListAsync(), nameof(AnneeAcad.Id), nameof(AnneeAcad.Libelle), annee?.Id);
        ViewData["FiliereId"] = new SelectList(await _context.Filieres.OrderBy(f => f.Nom).ToListAsync(), nameof(Filiere.Id), nameof(Filiere.Nom), filiereId);
        ViewData["NiveauId"] = new SelectList(await _context.Niveaux.OrderBy(n => n.Id).ToListAsync(), nameof(Niveau.Id), nameof(Niveau.Libelle), niveauId);
        ViewData["Decision"] = new SelectList(Decisions.Toutes, decision);

        if (annee == null) return View(new List<LigneDeliberation>());

        var lignes = await _context.CalculerAsync(annee.Id, filiereId, niveauId);
        // Les compteurs portent sur toute la sélection ; le filtre de décision ne réduit que le tableau
        ViewData["toutes"] = lignes;
        return View(string.IsNullOrEmpty(decision) ? lignes : lignes.Where(l => l.Bilan.Decision == decision).ToList());
    }

    public async Task<IActionResult> Exporter(int anneeId, int? filiereId, int? niveauId, string? decision)
    {
        var lignes = await _context.CalculerAsync(anneeId, filiereId, niveauId);
        if (!string.IsNullOrEmpty(decision)) lignes = lignes.Where(l => l.Bilan.Decision == decision).ToList();

        var contenu = Csv.Generer(
            ["Filière", "Niveau", "Rang", "Matricule", "Nom", "Prénom", "Moyenne", "Crédits validés", "Crédits", "UE sans note", "Décision"],
            lignes.Select(l => new object?[]
            {
                l.Inscription.Filiere?.Code, l.Inscription.Niveau?.Code, l.Rang, l.Inscription.Etudiant?.Matricule,
                l.Inscription.Etudiant?.Nom, l.Inscription.Etudiant?.Prenom, l.Bilan.Moyenne,
                l.Bilan.CreditsValides, l.Bilan.CreditsTotal, l.Bilan.UESansNote, l.Bilan.Decision
            }));
        return File(contenu, "text/csv", "deliberations.csv");
    }

    private async Task<AnneeAcad?> AnneeOuCouranteAsync(int? anneeId)
    {
        if (anneeId != null) return await _context.AnneesAcad.FindAsync(anneeId);
        var aujourdhui = DateTime.Today;
        return await _context.AnneesAcad.Where(a => a.DateDebut <= aujourdhui && a.DateFin >= aujourdhui).FirstOrDefaultAsync()
            ?? await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).FirstOrDefaultAsync();
    }
}
