using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

/// <summary>
/// Gestion des années académiques : calendrier qui encadre inscriptions, tarifs, séances et délibérations.
/// </summary>
public class AnneesAcadController : AppController
{
    /// <summary>Crée le contrôleur.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    public AnneesAcadController(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// Liste des années, de la plus récente à la plus ancienne, avec leurs inscriptions et encaissements.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var liste = await _context.AnneesAcad.AsNoTracking()
            .Include(a => a.Inscriptions).ThenInclude(i => i.Paiements)
            .OrderByDescending(a => a.DateDebut)
            .ToListAsync();
        return View(liste);
    }

    /// <summary>Affiche la fiche d'une année académique, avec ses inscriptions par filière et niveau.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    /// <returns>La fiche, ou 404 si l'élément n'existe pas.</returns>
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

    /// <summary>
    /// Affiche le formulaire de création, pré-rempli avec l'année qui suit la plus récente (par défaut du 1er octobre
    /// au 31 juillet).
    /// </summary>
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

    /// <summary>
    /// Enregistre l'année (la date de fin doit suivre la date de début), puis ouvre sa
    /// fiche ; en cas d'erreur, réaffiche le formulaire.
    /// </summary>
    /// <param name="annee">Valeurs saisies (seuls les champs du formulaire sont liés).</param>
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

    /// <summary>Affiche le formulaire de modification d'une année académique.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var annee = await _context.AnneesAcad.FindAsync(id);
        if (annee == null) return NotFound();

        return View("Formulaire", annee);
    }

    /// <summary>
    /// Enregistre les modifications d'une année académique, puis revient sur sa fiche ; en cas d'erreur, réaffiche le
    /// formulaire.
    /// </summary>
    /// <param name="id">Identifiant de l'élément.</param>
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

    /// <summary>
    /// Affiche la confirmation de suppression d'une année académique, avec ce qui l'empêche : les inscriptions qui y
    /// sont rattachées.
    /// </summary>
    /// <param name="id">Identifiant de l'élément.</param>
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var annee = await _context.AnneesAcad.FindAsync(id);
        if (annee == null) return NotFound();

        var inscriptions = await _context.Inscriptions.CountAsync(i => i.AnneeAcadId == id);
        return ConfirmerSuppression("Supprimer l'année académique", annee.Libelle, annee.Id,
            blocages: Si((inscriptions, $"{Formats.Pluriel(inscriptions, "inscription")} y {(inscriptions > 1 ? "sont rattachées" : "est rattachée")}.")));
    }

    /// <summary>Supprime l'année académique après confirmation, puis revient à la liste.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
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
