using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

/// <summary>Gestion des unités d'enseignement : code, intitulé, crédits, filière et niveau.</summary>
public class UEsController : AppController
{
    /// <summary>Crée le contrôleur.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    public UEsController(ApplicationDbContext context) : base(context) { }

    /// <summary>Liste des UE, filtrable.</summary>
    /// <param name="q">Recherche sur le code ou l'intitulé.</param>
    /// <param name="filiereId">Filtre par filière.</param>
    /// <param name="niveauId">Filtre par niveau.</param>
    public async Task<IActionResult> Index(string? q, int? filiereId, int? niveauId)
    {
        var requete = _context.UEs.AsNoTracking()
            .Include(u => u.Filiere)
            .Include(u => u.Niveau)
            .Include(u => u.Cours)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            requete = requete.Where(u => u.Code.Contains(q) || u.Intitule.Contains(q));
        }
        if (filiereId != null) requete = requete.Where(u => u.FiliereId == filiereId);
        if (niveauId != null) requete = requete.Where(u => u.NiveauId == niveauId);

        ViewData["q"] = q;
        ViewData["FiliereId"] = new SelectList(await _context.Filieres.OrderBy(f => f.Nom).ToListAsync(), nameof(Filiere.Id), nameof(Filiere.Nom), filiereId);
        ViewData["NiveauId"] = new SelectList(await _context.Niveaux.OrderBy(n => n.Id).ToListAsync(), nameof(Niveau.Id), nameof(Niveau.Libelle), niveauId);

        var liste = await requete.OrderBy(u => u.Filiere!.Code).ThenBy(u => u.NiveauId).ThenBy(u => u.Code).ToListAsync();
        return View(liste);
    }

    /// <summary>Affiche la fiche d'une UE, avec ses cours.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    /// <returns>La fiche, ou 404 si l'élément n'existe pas.</returns>
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var ue = await _context.UEs.AsNoTracking()
            .Include(u => u.Filiere)
            .Include(u => u.Niveau)
            .Include(u => u.Cours).ThenInclude(c => c.Enseignant)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (ue == null) return NotFound();

        return View(ue);
    }

    /// <summary>Affiche le formulaire de création d'une UE.</summary>
    /// <param name="filiereId">Filière pré-sélectionnée.</param>
    /// <param name="niveauId">Niveau pré-sélectionné.</param>
    public async Task<IActionResult> Create(int? filiereId, int? niveauId)
    {
        var ue = new UE { Credits = 6, FiliereId = filiereId ?? 0, NiveauId = niveauId ?? 0 };
        await RemplirListesAsync(ue);
        return View("Formulaire", ue);
    }

    /// <summary>
    /// Enregistre l'UE (code mis en majuscules), puis ouvre sa fiche ; en cas d'erreur,
    /// réaffiche le formulaire.
    /// </summary>
    /// <param name="ue">Valeurs saisies (seuls les champs du formulaire sont liés).</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Code,Intitule,Credits,FiliereId,NiveauId")] UE ue)
    {
        if (ModelState.IsValid)
        {
            ue.Code = ue.Code.Trim().ToUpperInvariant();
            _context.Add(ue);
            if (await EnregistrerAsync())
            {
                Succes($"UE {ue.Code} créée.");
                return RedirectToAction(nameof(Details), new { id = ue.Id });
            }
        }
        await RemplirListesAsync(ue);
        return View("Formulaire", ue);
    }

    /// <summary>Affiche le formulaire de modification d'une UE.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var ue = await _context.UEs.FindAsync(id);
        if (ue == null) return NotFound();

        await RemplirListesAsync(ue);
        return View("Formulaire", ue);
    }

    /// <summary>
    /// Enregistre les modifications d'une UE, puis revient sur sa fiche ; en cas d'erreur, réaffiche le formulaire.
    /// </summary>
    /// <param name="id">Identifiant de l'élément.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id)
    {
        var ue = await _context.UEs.FindAsync(id);
        if (ue == null) return NotFound();

        if (await TryUpdateModelAsync(ue, string.Empty, u => u.Code, u => u.Intitule, u => u.Credits, u => u.FiliereId, u => u.NiveauId))
        {
            ue.Code = ue.Code.Trim().ToUpperInvariant();
            if (await EnregistrerAsync())
            {
                Succes("Unité d'enseignement mise à jour.");
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        await RemplirListesAsync(ue);
        return View("Formulaire", ue);
    }

    /// <summary>
    /// Affiche la confirmation de suppression d'une UE, avec ce qui l'empêche : les séances programmées pour ses
    /// cours ; ses cours sont supprimés avec elle.
    /// </summary>
    /// <param name="id">Identifiant de l'élément.</param>
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var ue = await _context.UEs.FindAsync(id);
        if (ue == null) return NotFound();

        // OnDelete(Cascade) : les cours de l'UE partent avec elle
        var cours = await _context.Cours.CountAsync(c => c.UEId == id);
        var seances = await _context.Seances.CountAsync(s => s.Cours!.UEId == id);
        return ConfirmerSuppression("Supprimer l'unité d'enseignement", $"{ue.Code} — {ue.Intitule}", ue.Id,
            blocages: Si((seances, $"{Formats.Pluriel(seances, "séance programmée", "séances programmées")} pour ses cours à l'emploi du temps.")),
            effets: Si((cours, $"{Formats.Pluriel(cours, "cours")} {(cours > 1 ? "seront supprimés" : "sera supprimé")} avec elle.")));
    }

    /// <summary>Supprime l'UE après confirmation, puis revient à la liste.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var ue = await _context.UEs.FindAsync(id);
        if (ue == null) return RedirectToAction(nameof(Index));

        return await SupprimerAsync(ue, $"UE {ue.Code} supprimée.");
    }

    private async Task RemplirListesAsync(UE ue)
    {
        ViewData["FiliereId"] = new SelectList(await _context.Filieres.OrderBy(f => f.Nom).ToListAsync(), nameof(Filiere.Id), nameof(Filiere.Nom), ue.FiliereId);
        ViewData["NiveauId"] = new SelectList(await _context.Niveaux.OrderBy(n => n.Id).ToListAsync(), nameof(Niveau.Id), nameof(Niveau.Libelle), ue.NiveauId);
    }
}
