using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

// Grille de saisie des notes : une UE, une année, tous les étudiants inscrits (inscriptions validées)
public class NotesController : AppController
{
    public NotesController(ApplicationDbContext context) : base(context) { }

    [HttpGet]
    public async Task<IActionResult> Index(int? anneeId, int? ueId)
    {
        if (anneeId == null)
        {
            var aujourdhui = DateTime.Today;
            var annee = await _context.AnneesAcad.Where(a => a.DateDebut <= aujourdhui && a.DateFin >= aujourdhui).FirstOrDefaultAsync()
                ?? await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).FirstOrDefaultAsync();
            anneeId = annee?.Id;
        }
        return await AfficherAsync(anneeId, ueId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(int anneeId, int ueId, List<SaisieNote> lignes)
    {
        var ue = await _context.UEs.FindAsync(ueId);
        if (ue == null) return NotFound();

        // Seules les inscriptions de la grille sont acceptées, quel que soit le contenu du formulaire
        var inscrits = await _context.Inscriptions
            .Where(i => i.AnneeAcadId == anneeId && i.FiliereId == ue.FiliereId && i.NiveauId == ue.NiveauId && i.Statut == Inscription.Validee)
            .Select(i => i.Id)
            .ToListAsync();

        if (!ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, "Rien n'a été enregistré : corrigez les notes signalées (de 0 à 20).");
            return await AfficherAsync(anneeId, ueId, lignes);
        }

        var existantes = await _context.Notes
            .Where(n => n.UEId == ueId && inscrits.Contains(n.InscriptionId))
            .ToDictionaryAsync(n => n.InscriptionId);

        foreach (var ligne in lignes.Where(l => inscrits.Contains(l.InscriptionId)))
        {
            existantes.TryGetValue(ligne.InscriptionId, out var note);
            var vide = ligne.Valeur == null && ligne.Rattrapage == null;

            if (note == null && !vide)
            {
                _context.Notes.Add(new Note { InscriptionId = ligne.InscriptionId, UEId = ueId, Valeur = ligne.Valeur, Rattrapage = ligne.Rattrapage });
            }
            else if (note != null && vide)
            {
                _context.Notes.Remove(note);
            }
            else if (note != null)
            {
                note.Valeur = ligne.Valeur;
                note.Rattrapage = ligne.Rattrapage;
            }
        }

        if (await EnregistrerAsync())
        {
            Succes("Notes enregistrées.");
            return RedirectToAction(nameof(Index), new { anneeId, ueId });
        }
        return await AfficherAsync(anneeId, ueId, lignes);
    }

    private async Task<IActionResult> AfficherAsync(int? anneeId, int? ueId, List<SaisieNote>? saisie = null)
    {
        var ues = await _context.UEs.AsNoTracking().Include(u => u.Filiere).Include(u => u.Niveau)
            .OrderBy(u => u.Filiere!.Code).ThenBy(u => u.NiveauId).ThenBy(u => u.Code)
            .ToListAsync();

        ViewData["AnneeId"] = new SelectList(await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).ToListAsync(), nameof(AnneeAcad.Id), nameof(AnneeAcad.Libelle), anneeId);
        ViewData["UEId"] = ues
            .Select(u => new SelectListItem($"{u.Filiere?.Code} {u.Niveau?.Code} · {u}", u.Id.ToString(), u.Id == ueId))
            .ToList();
        ViewData["anneeChoisie"] = anneeId;

        var ue = ues.FirstOrDefault(u => u.Id == ueId);
        ViewData["ue"] = ue;
        if (ue == null || anneeId == null) return View(new List<LigneNote>());

        var inscriptions = await _context.Inscriptions.AsNoTracking()
            .Include(i => i.Etudiant)
            .Where(i => i.AnneeAcadId == anneeId && i.FiliereId == ue.FiliereId && i.NiveauId == ue.NiveauId && i.Statut == Inscription.Validee)
            .OrderBy(i => i.Etudiant!.Nom).ThenBy(i => i.Etudiant!.Prenom)
            .ToListAsync();
        var notes = await _context.Notes.AsNoTracking()
            .Where(n => n.UEId == ue.Id && n.Inscription!.AnneeAcadId == anneeId)
            .ToDictionaryAsync(n => n.InscriptionId);

        var grille = inscriptions.Select(i =>
        {
            notes.TryGetValue(i.Id, out var note);
            if (saisie == null) return new LigneNote(i, note?.Valeur, note?.Rattrapage, note?.Retenue);

            // Après une erreur, la saisie prime sur la base, y compris une case vidée
            var ligne = saisie.FirstOrDefault(s => s.InscriptionId == i.Id);
            return new LigneNote(i, ligne?.Valeur, ligne?.Rattrapage, note?.Retenue);
        }).ToList();

        return View(grille);
    }
}

public sealed record LigneNote(Inscription Inscription, decimal? Valeur, decimal? Rattrapage, decimal? Retenue);
