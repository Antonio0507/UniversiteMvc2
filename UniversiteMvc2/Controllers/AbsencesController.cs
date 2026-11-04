using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

// Assiduité : feuille d'appel par séance, et récapitulatif des absences par étudiant sur une année
public class AbsencesController : AppController
{
    public AbsencesController(ApplicationDbContext context) : base(context) { }

    public async Task<IActionResult> Index(int? anneeId, int? filiereId, int? niveauId, string? q)
    {
        var annee = await AnneeOuCouranteAsync(anneeId);
        ViewData["annee"] = annee;
        ViewData["q"] = q?.Trim();
        ViewData["AnneeId"] = new SelectList(await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).ToListAsync(), nameof(AnneeAcad.Id), nameof(AnneeAcad.Libelle), annee?.Id);
        ViewData["FiliereId"] = new SelectList(await _context.Filieres.OrderBy(f => f.Nom).ToListAsync(), nameof(Filiere.Id), nameof(Filiere.Nom), filiereId);
        ViewData["NiveauId"] = new SelectList(await _context.Niveaux.OrderBy(n => n.Id).ToListAsync(), nameof(Niveau.Id), nameof(Niveau.Libelle), niveauId);

        return View(annee == null ? new List<LigneAssiduite>() : await RecapitulatifAsync(annee.Id, filiereId, niveauId, q));
    }

    public async Task<IActionResult> Exporter(int anneeId, int? filiereId, int? niveauId, string? q)
    {
        var lignes = await RecapitulatifAsync(anneeId, filiereId, niveauId, q);
        var contenu = Csv.Generer(
            ["Matricule", "Nom", "Prénom", "Filière", "Niveau", "Absences", "Heures", "Heures non justifiées"],
            lignes.Select(l => new object?[]
            {
                l.Inscription.Etudiant?.Matricule, l.Inscription.Etudiant?.Nom, l.Inscription.Etudiant?.Prenom,
                l.Inscription.Filiere?.Code, l.Inscription.Niveau?.Code, l.Absences, l.Heures, l.HeuresNonJustifiees
            }));
        return File(contenu, "text/csv", "assiduite.csv");
    }

    // Lien depuis la fiche d'une séance : une absence se consulte et se corrige sur la feuille d'appel
    public async Task<IActionResult> Details(int? id)
    {
        var absence = id == null ? null : await _context.Absences.FindAsync(id);
        if (absence == null) return NotFound();
        return RedirectToAction(nameof(Appel), new { seanceId = absence.SeanceId });
    }

    [HttpGet]
    public async Task<IActionResult> Appel(int seanceId) => await AfficherAppelAsync(seanceId);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Appel(int seanceId, List<SaisieAbsence> lignes)
    {
        var seance = await ChargerSeanceAsync(seanceId);
        if (seance == null) return NotFound();

        if (!ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, "Rien n'a été enregistré : corrigez les motifs signalés.");
            return await AfficherAppelAsync(seanceId, lignes);
        }

        // Seuls les inscrits de la feuille sont acceptés, quel que soit le contenu du formulaire
        var inscrits = (await InscritsAsync(seance)).Select(i => i.Id).ToHashSet();
        var existantes = await _context.Absences.Where(a => a.SeanceId == seanceId).ToDictionaryAsync(a => a.InscriptionId);

        foreach (var ligne in lignes.Where(l => inscrits.Contains(l.InscriptionId)))
        {
            existantes.TryGetValue(ligne.InscriptionId, out var absence);
            var motif = string.IsNullOrWhiteSpace(ligne.Motif) ? null : ligne.Motif.Trim();

            if (absence == null && ligne.Absent)
            {
                _context.Absences.Add(new Absence { SeanceId = seanceId, InscriptionId = ligne.InscriptionId, Justifiee = ligne.Justifiee, Motif = motif });
            }
            else if (absence != null && !ligne.Absent)
            {
                _context.Absences.Remove(absence);
            }
            else if (absence != null)
            {
                absence.Justifiee = ligne.Justifiee;
                absence.Motif = motif;
            }
        }

        if (await EnregistrerAsync())
        {
            Succes("Appel enregistré.");
            return RedirectToAction(nameof(Appel), new { seanceId });
        }
        return await AfficherAppelAsync(seanceId, lignes);
    }

    private async Task<IActionResult> AfficherAppelAsync(int seanceId, List<SaisieAbsence>? saisie = null)
    {
        var seance = await ChargerSeanceAsync(seanceId);
        if (seance == null) return NotFound();

        var inscrits = await InscritsAsync(seance);
        var absences = await _context.Absences.AsNoTracking().Where(a => a.SeanceId == seanceId).ToDictionaryAsync(a => a.InscriptionId);

        // Après une erreur, la saisie prime sur la base
        var feuille = inscrits.Select(i =>
        {
            var ligne = saisie?.FirstOrDefault(s => s.InscriptionId == i.Id);
            if (ligne != null) return new LigneAppel(i, ligne.Absent, ligne.Justifiee, ligne.Motif);
            absences.TryGetValue(i.Id, out var absence);
            return new LigneAppel(i, absence != null, absence?.Justifiee ?? false, absence?.Motif);
        }).ToList();

        ViewData["seance"] = seance;
        ViewData["annee"] = await AnneeDeAsync(seance.Date);
        return View("Appel", feuille);
    }

    private Task<Seance?> ChargerSeanceAsync(int id) =>
        _context.Seances.AsNoTracking()
            .Include(s => s.Cours).ThenInclude(c => c!.UE).ThenInclude(u => u!.Filiere)
            .Include(s => s.Cours).ThenInclude(c => c!.UE).ThenInclude(u => u!.Niveau)
            .Include(s => s.Cours).ThenInclude(c => c!.Enseignant)
            .Include(s => s.Salle)
            .FirstOrDefaultAsync(s => s.Id == id);

    // Étudiants attendus : inscriptions validées de l'année de la séance, dans la filière et le niveau de l'UE
    private async Task<List<Inscription>> InscritsAsync(Seance seance)
    {
        var annee = await AnneeDeAsync(seance.Date);
        var ue = seance.Cours?.UE;
        if (annee == null || ue == null) return [];

        return await _context.Inscriptions.AsNoTracking()
            .Include(i => i.Etudiant)
            .Where(i => i.AnneeAcadId == annee.Id && i.FiliereId == ue.FiliereId && i.NiveauId == ue.NiveauId && i.Statut == Inscription.Validee)
            .OrderBy(i => i.Etudiant!.Nom).ThenBy(i => i.Etudiant!.Prenom)
            .ToListAsync();
    }

    private async Task<List<LigneAssiduite>> RecapitulatifAsync(int anneeId, int? filiereId, int? niveauId, string? q)
    {
        var requete = _context.Absences.AsNoTracking()
            .Include(a => a.Seance)
            .Include(a => a.Inscription).ThenInclude(i => i!.Etudiant)
            .Include(a => a.Inscription).ThenInclude(i => i!.Filiere)
            .Include(a => a.Inscription).ThenInclude(i => i!.Niveau)
            .Where(a => a.Inscription!.AnneeAcadId == anneeId);
        if (filiereId != null) requete = requete.Where(a => a.Inscription!.FiliereId == filiereId);
        if (niveauId != null) requete = requete.Where(a => a.Inscription!.NiveauId == niveauId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            requete = requete.Where(a => a.Inscription!.Etudiant!.Nom.Contains(q) || a.Inscription.Etudiant.Prenom.Contains(q) || a.Inscription.Etudiant.Matricule.Contains(q));
        }

        var absences = await requete.AsSplitQuery().ToListAsync();
        return absences
            .GroupBy(a => a.InscriptionId)
            .Select(g => new LigneAssiduite(
                g.First().Inscription!,
                g.Count(),
                g.Sum(a => a.Seance!.Duree),
                g.Where(a => !a.Justifiee).Sum(a => a.Seance!.Duree)))
            .OrderByDescending(l => l.HeuresNonJustifiees).ThenByDescending(l => l.Heures).ThenBy(l => l.Inscription.Etudiant?.Nom)
            .ToList();
    }

    private Task<AnneeAcad?> AnneeDeAsync(DateTime date) =>
        _context.AnneesAcad.AsNoTracking().FirstOrDefaultAsync(a => a.DateDebut <= date && a.DateFin >= date);

    private async Task<AnneeAcad?> AnneeOuCouranteAsync(int? anneeId)
    {
        if (anneeId != null) return await _context.AnneesAcad.FindAsync(anneeId);
        return await AnneeDeAsync(DateTime.Today)
            ?? await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).FirstOrDefaultAsync();
    }
}

public sealed record LigneAppel(Inscription Inscription, bool Absent, bool Justifiee, string? Motif);

public sealed record LigneAssiduite(Inscription Inscription, int Absences, decimal Heures, decimal HeuresNonJustifiees);
