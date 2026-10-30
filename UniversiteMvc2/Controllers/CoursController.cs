using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

public class CoursController : AppController
{
    // Valeur du filtre « enseignant » pour les cours sans titulaire
    public const int NonAttribue = 0;

    public CoursController(ApplicationDbContext context) : base(context) { }

    public async Task<IActionResult> Index(string? q, int? enseignantId, int? ueId)
    {
        var requete = _context.Cours.AsNoTracking()
            .Include(c => c.UE).ThenInclude(u => u!.Filiere)
            .Include(c => c.UE).ThenInclude(u => u!.Niveau)
            .Include(c => c.Enseignant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            requete = requete.Where(c => c.Intitule.Contains(q) || c.UE!.Code.Contains(q));
        }
        if (enseignantId == NonAttribue) requete = requete.Where(c => c.EnseignantId == null);
        else if (enseignantId != null) requete = requete.Where(c => c.EnseignantId == enseignantId);
        if (ueId != null) requete = requete.Where(c => c.UEId == ueId);

        var enseignants = await _context.Enseignants.OrderBy(e => e.Nom).ToListAsync();
        var choixEnseignants = new List<SelectListItem> { new("— Non attribués —", NonAttribue.ToString(), enseignantId == NonAttribue) };
        choixEnseignants.AddRange(enseignants.Select(e => new SelectListItem(e.NomComplet, e.Id.ToString(), e.Id == enseignantId)));

        ViewData["q"] = q;
        ViewData["EnseignantId"] = choixEnseignants;
        ViewData["UEId"] = await ListeUEsAsync(ueId);

        var liste = await requete.OrderBy(c => c.UE!.Code).ThenBy(c => c.Intitule).ToListAsync();
        return View(liste);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var cours = await _context.Cours.AsNoTracking()
            .Include(c => c.UE).ThenInclude(u => u!.Filiere)
            .Include(c => c.UE).ThenInclude(u => u!.Niveau)
            .Include(c => c.Enseignant)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cours == null) return NotFound();

        return View(cours);
    }

    public async Task<IActionResult> Create(int? ueId, int? enseignantId)
    {
        var cours = new Cours { VolumeHoraire = 30, UEId = ueId ?? 0, EnseignantId = enseignantId };
        await RemplirListesAsync(cours);
        return View("Formulaire", cours);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Intitule,VolumeHoraire,UEId,EnseignantId")] Cours cours)
    {
        if (ModelState.IsValid)
        {
            _context.Add(cours);
            if (await EnregistrerAsync())
            {
                Succes($"Cours « {cours.Intitule} » créé.");
                return RedirectToAction(nameof(Details), new { id = cours.Id });
            }
        }
        await RemplirListesAsync(cours);
        return View("Formulaire", cours);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var cours = await _context.Cours.FindAsync(id);
        if (cours == null) return NotFound();

        await RemplirListesAsync(cours);
        return View("Formulaire", cours);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id)
    {
        var cours = await _context.Cours.FindAsync(id);
        if (cours == null) return NotFound();

        if (await TryUpdateModelAsync(cours, string.Empty, c => c.Intitule, c => c.VolumeHoraire, c => c.UEId, c => c.EnseignantId)
            && await EnregistrerAsync())
        {
            Succes("Cours mis à jour.");
            return RedirectToAction(nameof(Details), new { id });
        }
        await RemplirListesAsync(cours);
        return View("Formulaire", cours);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var cours = await _context.Cours.Include(c => c.UE).FirstOrDefaultAsync(c => c.Id == id);
        if (cours == null) return NotFound();

        return ConfirmerSuppression("Supprimer le cours", $"{cours.Intitule} ({cours.UE?.Code})", cours.Id);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var cours = await _context.Cours.FindAsync(id);
        if (cours == null) return RedirectToAction(nameof(Index));

        return await SupprimerAsync(cours, $"Cours « {cours.Intitule} » supprimé.");
    }

    private async Task<List<SelectListItem>> ListeUEsAsync(int? selection) =>
        (await _context.UEs.OrderBy(u => u.Code).ToListAsync())
            .Select(u => new SelectListItem($"{u.Code} — {u.Intitule}", u.Id.ToString(), u.Id == selection))
            .ToList();

    private async Task RemplirListesAsync(Cours cours)
    {
        ViewData["UEId"] = await ListeUEsAsync(cours.UEId);
        ViewData["EnseignantId"] = new SelectList(await _context.Enseignants.OrderBy(e => e.Nom).ToListAsync(), nameof(Enseignant.Id), nameof(Enseignant.NomComplet), cours.EnseignantId);
    }
}
