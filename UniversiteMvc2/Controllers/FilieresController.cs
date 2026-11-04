using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

/// <summary>Gestion des filières (Informatique, Gestion, Droit…).</summary>
public class FilieresController : AppController
{
    /// <summary>Crée le contrôleur.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    public FilieresController(ApplicationDbContext context) : base(context) { }

    /// <summary>Liste des filières, avec leurs UE et leurs inscriptions.</summary>
    public async Task<IActionResult> Index()
    {
        var liste = await _context.Filieres.AsNoTracking()
            .Include(f => f.UEs)
            .Include(f => f.Inscriptions)
            .AsSplitQuery()
            .OrderBy(f => f.Code)
            .ToListAsync();
        return View(liste);
    }

    /// <summary>Affiche la fiche d'une filière, avec ses UE par niveau et ses inscriptions.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    /// <returns>La fiche, ou 404 si l'élément n'existe pas.</returns>
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var filiere = await _context.Filieres.AsNoTracking()
            .Include(f => f.UEs).ThenInclude(u => u.Niveau)
            .Include(f => f.UEs).ThenInclude(u => u.Cours)
            .Include(f => f.Inscriptions).ThenInclude(i => i.AnneeAcad)
            .AsSplitQuery()
            .FirstOrDefaultAsync(f => f.Id == id);
        if (filiere == null) return NotFound();

        return View(filiere);
    }

    /// <summary>Affiche le formulaire de création d'une filière.</summary>
    public IActionResult Create() => View("Formulaire", new Filiere());

    /// <summary>
    /// Enregistre la filière (code mis en majuscules), puis ouvre sa fiche ; en cas
    /// d'erreur, réaffiche le formulaire.
    /// </summary>
    /// <param name="filiere">Valeurs saisies (seuls les champs du formulaire sont liés).</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Code,Nom")] Filiere filiere)
    {
        if (ModelState.IsValid)
        {
            filiere.Code = filiere.Code.Trim().ToUpperInvariant();
            _context.Add(filiere);
            if (await EnregistrerAsync())
            {
                Succes($"Filière {filiere.Nom} créée.");
                return RedirectToAction(nameof(Details), new { id = filiere.Id });
            }
        }
        return View("Formulaire", filiere);
    }

    /// <summary>Affiche le formulaire de modification d'une filière.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var filiere = await _context.Filieres.FindAsync(id);
        if (filiere == null) return NotFound();

        return View("Formulaire", filiere);
    }

    /// <summary>
    /// Enregistre les modifications d'une filière, puis revient sur sa fiche ; en cas d'erreur, réaffiche le
    /// formulaire.
    /// </summary>
    /// <param name="id">Identifiant de l'élément.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id)
    {
        var filiere = await _context.Filieres.FindAsync(id);
        if (filiere == null) return NotFound();

        if (await TryUpdateModelAsync(filiere, string.Empty, f => f.Code, f => f.Nom))
        {
            filiere.Code = filiere.Code.Trim().ToUpperInvariant();
            if (await EnregistrerAsync())
            {
                Succes("Filière mise à jour.");
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        return View("Formulaire", filiere);
    }

    /// <summary>
    /// Affiche la confirmation de suppression d'une filière, avec ce qui l'empêche : les UE et les inscriptions qui
    /// en dépendent.
    /// </summary>
    /// <param name="id">Identifiant de l'élément.</param>
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var filiere = await _context.Filieres.FindAsync(id);
        if (filiere == null) return NotFound();

        var ues = await _context.UEs.CountAsync(u => u.FiliereId == id);
        var inscriptions = await _context.Inscriptions.CountAsync(i => i.FiliereId == id);
        return ConfirmerSuppression("Supprimer la filière", $"{filiere.Code} — {filiere.Nom}", filiere.Id,
            blocages: Si(
                (ues, $"{Formats.Pluriel(ues, "unité d'enseignement", "unités d'enseignement")} en dépendent."),
                (inscriptions, $"{Formats.Pluriel(inscriptions, "inscription")} y {(inscriptions > 1 ? "sont rattachées" : "est rattachée")}.")));
    }

    /// <summary>Supprime la filière après confirmation, puis revient à la liste.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var filiere = await _context.Filieres.FindAsync(id);
        if (filiere == null) return RedirectToAction(nameof(Index));

        return await SupprimerAsync(filiere, $"Filière {filiere.Nom} supprimée.");
    }
}
