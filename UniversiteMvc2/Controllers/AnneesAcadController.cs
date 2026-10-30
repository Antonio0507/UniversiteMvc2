using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

public class AnneesAcadController : AppController
{
    public AnneesAcadController(ApplicationDbContext context) : base(context) { }

    public async Task<IActionResult> Index()
    {
        var liste = await _context.AnneesAcad.AsNoTracking()
            .Include(a => a.Inscriptions).ThenInclude(i => i.Paiements)
            .OrderByDescending(a => a.DateDebut)
            .ToListAsync();
        return View(liste);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var annee = await _context.AnneesAcad.AsNoTracking()
            .Include(a => a.Inscriptions).ThenInclude(i => i.Filiere)
            .Include(a => a.Inscriptions).ThenInclude(i => i.Niveau)
            .Include(a => a.Inscriptions).ThenInclude(i => i.Paiements)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id);
        if (annee == null) return NotFound();

        return View(annee);
    }

    public async Task<IActionResult> Create()
    {
        // Propose l'année qui suit la plus récente
        var derniere = await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).FirstOrDefaultAsync();
        var debut = derniere?.DateDebut.AddYears(1) ?? new DateTime(DateTime.Today.Year, 10, 1);
        return View("Formulaire", new AnneeAcad
        {
            Libelle = $"{debut.Year}-{debut.Year + 1}",
            DateDebut = debut,
            DateFin = derniere?.DateFin.AddYears(1) ?? new DateTime(debut.Year + 1, 7, 31)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Libelle,DateDebut,DateFin")] AnneeAcad annee)
    {
        VerifierDates(annee);
        if (ModelState.IsValid)
        {
            _context.Add(annee);
            if (await EnregistrerAsync())
            {
                Succes($"Année {annee.Libelle} ouverte.");
                return RedirectToAction(nameof(Details), new { id = annee.Id });
            }
        }
        return View("Formulaire", annee);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var annee = await _context.AnneesAcad.FindAsync(id);
        if (annee == null) return NotFound();

        return View("Formulaire", annee);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id)
    {
        var annee = await _context.AnneesAcad.FindAsync(id);
        if (annee == null) return NotFound();

        if (await TryUpdateModelAsync(annee, string.Empty, a => a.Libelle, a => a.DateDebut, a => a.DateFin))
        {
            VerifierDates(annee);
            if (ModelState.IsValid && await EnregistrerAsync())
            {
                Succes("Année académique mise à jour.");
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        return View("Formulaire", annee);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var annee = await _context.AnneesAcad.FindAsync(id);
        if (annee == null) return NotFound();

        var inscriptions = await _context.Inscriptions.CountAsync(i => i.AnneeAcadId == id);
        return ConfirmerSuppression("Supprimer l'année académique", annee.Libelle, annee.Id,
            blocages: Si((inscriptions, $"{Formats.Pluriel(inscriptions, "inscription")} y {(inscriptions > 1 ? "sont rattachées" : "est rattachée")}.")));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var annee = await _context.AnneesAcad.FindAsync(id);
        if (annee == null) return RedirectToAction(nameof(Index));

        return await SupprimerAsync(annee, $"Année {annee.Libelle} supprimée.");
    }

    private void VerifierDates(AnneeAcad annee)
    {
        if (annee.DateFin <= annee.DateDebut)
            ModelState.AddModelError(nameof(AnneeAcad.DateFin), "La date de fin doit suivre la date de début.");
    }
}
