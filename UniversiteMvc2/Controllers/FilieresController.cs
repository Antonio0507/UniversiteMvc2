using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

public class FilieresController : AppController
{
    public FilieresController(ApplicationDbContext context) : base(context) { }

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

    public IActionResult Create() => View("Formulaire", new Filiere());

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

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var filiere = await _context.Filieres.FindAsync(id);
        if (filiere == null) return NotFound();

        return View("Formulaire", filiere);
    }

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

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var filiere = await _context.Filieres.FindAsync(id);
        if (filiere == null) return RedirectToAction(nameof(Index));

        return await SupprimerAsync(filiere, $"Filière {filiere.Nom} supprimée.");
    }
}
