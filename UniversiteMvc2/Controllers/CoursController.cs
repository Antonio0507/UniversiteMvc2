using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

/// <summary>Gestion des cours : parties d'une UE (CM, TD, TP), avec volume horaire et enseignant.</summary>
public class CoursController : AppController
{
    /// <summary>Valeur du filtre « enseignant » pour les cours sans titulaire</summary>
    public const int NonAttribue = 0;

    /// <summary>Crée le contrôleur.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    public CoursController(ApplicationDbContext context) : base(context) { }

    /// <summary>Liste des cours, filtrable.</summary>
    /// <param name="q">Recherche sur l'intitulé ou le code d'UE.</param>
    /// <param name="enseignantId">Filtre par enseignant ; NonAttribue pour les cours sans enseignant.</param>
    /// <param name="ueId">Filtre par UE.</param>
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

    /// <summary>Affiche la fiche d'un cours.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    /// <returns>La fiche, ou 404 si l'élément n'existe pas.</returns>
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

    /// <summary>Affiche le formulaire de création d'un cours, avec 30 h par défaut.</summary>
    /// <param name="ueId">UE pré-sélectionnée, facultative.</param>
    /// <param name="enseignantId">Enseignant pré-sélectionné, facultatif.</param>
    public async Task<IActionResult> Create(int? ueId, int? enseignantId)
    {
        var cours = new Cours { VolumeHoraire = 30, UEId = ueId ?? 0, EnseignantId = enseignantId };
        await RemplirListesAsync(cours);
        return View("Formulaire", cours);
    }

    /// <summary>
    /// Enregistre le cours, puis ouvre sa fiche ; en cas d'erreur, réaffiche le
    /// formulaire.
    /// </summary>
    /// <param name="cours">Valeurs saisies (seuls les champs du formulaire sont liés).</param>
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

    /// <summary>Affiche le formulaire de modification d'un cours.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var cours = await _context.Cours.FindAsync(id);
        if (cours == null) return NotFound();

        await RemplirListesAsync(cours);
        return View("Formulaire", cours);
    }

    /// <summary>
    /// Enregistre les modifications d'un cours, puis revient sur sa fiche ; en cas d'erreur, réaffiche le formulaire.
    /// </summary>
    /// <param name="id">Identifiant de l'élément.</param>
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

    /// <summary>
    /// Affiche la confirmation de suppression d'un cours, avec ce qui l'empêche : les séances programmées à l'emploi
    /// du temps.
    /// </summary>
    /// <param name="id">Identifiant de l'élément.</param>
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var cours = await _context.Cours.Include(c => c.UE).FirstOrDefaultAsync(c => c.Id == id);
        if (cours == null) return NotFound();

        var seances = await _context.Seances.CountAsync(s => s.CoursId == id);
        return ConfirmerSuppression("Supprimer le cours", $"{cours.Intitule} ({cours.UE?.Code})", cours.Id,
            blocages: Si((seances, $"{Formats.Pluriel(seances, "séance programmée", "séances programmées")} à l'emploi du temps.")));
    }

    /// <summary>Supprime le cours après confirmation, puis revient à la liste.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
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
