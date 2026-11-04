using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

// Emploi du temps et calendrier des examens : une semaine à la fois, filtrable par promotion, enseignant ou salle.
// Formulaire et fiche : vues génériques (Views/Shared/Generique).
public class SeancesController : AppController
{
    public SeancesController(ApplicationDbContext context) : base(context) { }

    public async Task<IActionResult> Index(DateTime? semaine, int? filiereId, int? niveauId, int? enseignantId, int? salleId, string? type)
    {
        var lundi = Lundi(semaine ?? DateTime.Today);
        var dimanche = lundi.AddDays(7);

        var requete = Charger(_context.Seances.AsNoTracking())
            .Where(s => s.Date >= lundi && s.Date < dimanche);
        if (filiereId != null) requete = requete.Where(s => s.Cours!.UE!.FiliereId == filiereId);
        if (niveauId != null) requete = requete.Where(s => s.Cours!.UE!.NiveauId == niveauId);
        if (enseignantId != null) requete = requete.Where(s => s.Cours!.EnseignantId == enseignantId);
        if (salleId != null) requete = requete.Where(s => s.SalleId == salleId);
        if (!string.IsNullOrEmpty(type)) requete = requete.Where(s => s.Type == type);

        ViewData["lundi"] = lundi;
        ViewData["FiliereId"] = new SelectList(await _context.Filieres.OrderBy(f => f.Nom).ToListAsync(), nameof(Filiere.Id), nameof(Filiere.Nom), filiereId);
        ViewData["NiveauId"] = new SelectList(await _context.Niveaux.OrderBy(n => n.Id).ToListAsync(), nameof(Niveau.Id), nameof(Niveau.Code), niveauId);
        ViewData["EnseignantId"] = new SelectList(await _context.Enseignants.OrderBy(e => e.Nom).ToListAsync(), nameof(Enseignant.Id), nameof(Enseignant.NomComplet), enseignantId);
        ViewData["SalleId"] = new SelectList(await _context.Salles.OrderBy(s => s.Code).ToListAsync(), nameof(Salle.Id), nameof(Salle.Code), salleId);
        ViewData["Type"] = new SelectList(Seance.Types, type);

        var liste = await requete.OrderBy(s => s.Date).ThenBy(s => s.HeureDebut).ToListAsync();
        return View(liste);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var seance = await Charger(_context.Seances.AsNoTrackingWithIdentityResolution())
            .Include(s => s.Absences).ThenInclude(a => a.Inscription).ThenInclude(i => i!.Etudiant)
            .Include(s => s.Absences).ThenInclude(a => a.Inscription).ThenInclude(i => i!.Filiere)
            .Include(s => s.Absences).ThenInclude(a => a.Inscription).ThenInclude(i => i!.Niveau)
            .Include(s => s.Absences).ThenInclude(a => a.Inscription).ThenInclude(i => i!.AnneeAcad)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id);
        if (seance == null) return NotFound();

        return View(seance);
    }

    public async Task<IActionResult> Create(int? coursId, DateTime? date)
    {
        var seance = new Seance { CoursId = coursId ?? 0, Date = date ?? DateTime.Today };
        await RemplirListesAsync(seance);
        return View("Formulaire", seance);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("CoursId,Type,Date,HeureDebut,HeureFin,SalleId")] Seance seance)
    {
        await VerifierAsync(seance);
        if (ModelState.IsValid)
        {
            _context.Add(seance);
            if (await EnregistrerAsync())
            {
                Succes($"Séance du {Formats.Date(seance.Date)} programmée.");
                return RedirectToAction(nameof(Index), new { semaine = seance.Date.ToString("yyyy-MM-dd") });
            }
        }
        await RemplirListesAsync(seance);
        return View("Formulaire", seance);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var seance = await _context.Seances.FindAsync(id);
        if (seance == null) return NotFound();

        await RemplirListesAsync(seance);
        return View("Formulaire", seance);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id)
    {
        var seance = await _context.Seances.FindAsync(id);
        if (seance == null) return NotFound();

        if (await TryUpdateModelAsync(seance, string.Empty,
                s => s.CoursId, s => s.Type, s => s.Date, s => s.HeureDebut, s => s.HeureFin, s => s.SalleId))
        {
            await VerifierAsync(seance);
            if (ModelState.IsValid && await EnregistrerAsync())
            {
                Succes("Séance mise à jour.");
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        await RemplirListesAsync(seance);
        return View("Formulaire", seance);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var seance = await _context.Seances.Include(s => s.Cours).FirstOrDefaultAsync(s => s.Id == id);
        if (seance == null) return NotFound();

        var absences = await _context.Absences.CountAsync(a => a.SeanceId == id);
        return ConfirmerSuppression("Supprimer la séance", seance.ToString(), seance.Id,
            effets: Si((absences, $"{Formats.Pluriel(absences, "absence relevée", "absences relevées")} à cette séance {(absences > 1 ? "seront supprimées" : "sera supprimée")} avec.")));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var seance = await _context.Seances.FindAsync(id);
        if (seance == null) return RedirectToAction(nameof(Index));

        return await SupprimerAsync(seance, $"Séance du {Formats.Date(seance.Date)} supprimée.");
    }

    public static DateTime Lundi(DateTime jour) => jour.Date.AddDays(-(((int)jour.DayOfWeek + 6) % 7));

    private static IQueryable<Seance> Charger(IQueryable<Seance> requete) =>
        requete
            .Include(s => s.Cours).ThenInclude(c => c!.UE).ThenInclude(u => u!.Filiere)
            .Include(s => s.Cours).ThenInclude(c => c!.UE).ThenInclude(u => u!.Niveau)
            .Include(s => s.Cours).ThenInclude(c => c!.Enseignant)
            .Include(s => s.Salle);

    // Horaires cohérents, et aucun chevauchement pour la salle, l'enseignant ou la promotion (filière + niveau)
    private async Task VerifierAsync(Seance seance)
    {
        if (seance.HeureFin <= seance.HeureDebut)
        {
            ModelState.AddModelError(nameof(Seance.HeureFin), "L'heure de fin doit suivre l'heure de début.");
            return;
        }

        var cours = await _context.Cours.AsNoTracking().Include(c => c.UE).FirstOrDefaultAsync(c => c.Id == seance.CoursId);
        if (cours?.UE == null) return;

        var memeJour = await Charger(_context.Seances.AsNoTracking())
            .Where(s => s.Date == seance.Date.Date && s.Id != seance.Id)
            .ToListAsync();

        foreach (var autre in memeJour.Where(seance.Chevauche))
        {
            var horaire = $"{Formats.Heure(autre.HeureDebut)}–{Formats.Heure(autre.HeureFin)}";
            if (seance.SalleId != null && autre.SalleId == seance.SalleId)
                ModelState.AddModelError(nameof(Seance.SalleId), $"La salle {autre.Salle?.Code} est déjà occupée de {horaire} ({autre.Cours?.Intitule}).");
            if (cours.EnseignantId != null && autre.Cours?.EnseignantId == cours.EnseignantId)
                ModelState.AddModelError(nameof(Seance.HeureDebut), $"{autre.Cours?.Enseignant?.NomComplet} assure déjà « {autre.Cours?.Intitule} » de {horaire}.");
            if (autre.Cours?.UE?.FiliereId == cours.UE.FiliereId && autre.Cours.UE.NiveauId == cours.UE.NiveauId)
                ModelState.AddModelError(nameof(Seance.HeureDebut), $"La promotion {autre.Cours.UE.Filiere?.Code} {autre.Cours.UE.Niveau?.Code} a déjà « {autre.Cours.Intitule} » de {horaire}.");
        }
    }

    private async Task RemplirListesAsync(Seance seance)
    {
        var cours = await _context.Cours.AsNoTracking()
            .Include(c => c.UE).ThenInclude(u => u!.Filiere)
            .Include(c => c.UE).ThenInclude(u => u!.Niveau)
            .OrderBy(c => c.UE!.Code).ThenBy(c => c.Intitule)
            .ToListAsync();
        ViewData[nameof(Seance.CoursId)] = cours
            .Select(c => new SelectListItem($"{c.UE?.Code} · {c.Intitule} ({c.UE?.Filiere?.Code} {c.UE?.Niveau?.Code})", c.Id.ToString(), c.Id == seance.CoursId))
            .ToList();
        ViewData[nameof(Seance.SalleId)] = (await _context.Salles.AsNoTracking().OrderBy(s => s.Code).ToListAsync())
            .Select(s => new SelectListItem($"{s} · {Formats.Pluriel(s.Capacite, "place")}", s.Id.ToString(), s.Id == seance.SalleId))
            .ToList();

        var types = Seance.Types.ToList();
        if (!types.Contains(seance.Type)) types.Add(seance.Type);
        ViewData[nameof(Seance.Type)] = new SelectList(types, seance.Type);
    }
}
