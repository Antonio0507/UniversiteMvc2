using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

public class InscriptionsController : AppController
{
    public InscriptionsController(ApplicationDbContext context) : base(context) { }

    private IQueryable<Inscription> Filtrer(string? q, int? anneeId, int? filiereId, string? statut)
    {
        var requete = _context.Inscriptions.AsNoTracking()
            .Include(i => i.Etudiant)
            .Include(i => i.Filiere)
            .Include(i => i.Niveau)
            .Include(i => i.AnneeAcad)
            .Include(i => i.Paiements)
            .AsSplitQuery();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            requete = requete.Where(i => i.Etudiant!.Nom.Contains(q) || i.Etudiant.Prenom.Contains(q) || i.Etudiant.Matricule.Contains(q));
        }
        if (anneeId != null) requete = requete.Where(i => i.AnneeAcadId == anneeId);
        if (filiereId != null) requete = requete.Where(i => i.FiliereId == filiereId);
        if (!string.IsNullOrEmpty(statut)) requete = requete.Where(i => i.Statut == statut);
        return requete;
    }

    public async Task<IActionResult> Index(string? q, int? anneeId, int? filiereId, string? statut, int page = 1)
    {
        var requete = Filtrer(q, anneeId, filiereId, statut);

        ViewData["q"] = q?.Trim();
        ViewData["AnneeId"] = new SelectList(await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).ToListAsync(), nameof(AnneeAcad.Id), nameof(AnneeAcad.Libelle), anneeId);
        ViewData["FiliereId"] = new SelectList(await _context.Filieres.OrderBy(f => f.Nom).ToListAsync(), nameof(Filiere.Id), nameof(Filiere.Nom), filiereId);
        ViewData["Statut"] = new SelectList(Inscription.Statuts, statut);

        var tri = requete.OrderByDescending(i => i.DateInscription).ThenByDescending(i => i.Id);
        return View(await ListePaginee<Inscription>.CreerAsync(tri, page));
    }

    public async Task<IActionResult> Exporter(string? q, int? anneeId, int? filiereId, string? statut)
    {
        var inscriptions = await Filtrer(q, anneeId, filiereId, statut)
            .OrderByDescending(i => i.DateInscription).ThenByDescending(i => i.Id)
            .ToListAsync();
        var contenu = Csv.Generer(
            ["Date", "Matricule", "Nom", "Prénom", "Filière", "Niveau", "Année", "Statut", "Versé"],
            inscriptions.Select(i => new object?[]
            {
                i.DateInscription, i.Etudiant?.Matricule, i.Etudiant?.Nom, i.Etudiant?.Prenom, i.Filiere?.Code, i.Niveau?.Code,
                i.AnneeAcad?.Libelle, i.Statut, i.Paiements.Sum(p => p.Montant)
            }));
        return File(contenu, "text/csv", "inscriptions.csv");
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var inscription = await ChargerAsync(id.Value);
        if (inscription == null) return NotFound();

        var tarif = await _context.TarifDeAsync(inscription);
        var aides = inscription.Aides.Sum(a => a.Montant);
        ViewData["tarif"] = tarif;
        ViewData["solde"] = new Solde(tarif == null ? null : Solde.Net(tarif.Value, aides), inscription.Paiements.Sum(p => p.Montant));

        // Toutes les UE du cursus, notées ou non
        var ues = await _context.UEs.AsNoTracking()
            .Where(u => u.FiliereId == inscription.FiliereId && u.NiveauId == inscription.NiveauId)
            .OrderBy(u => u.Code)
            .ToListAsync();
        ViewData["ues"] = ues;
        ViewData["bilan"] = Bilan.Calculer(ues
            .Select(u => (u.Credits, inscription.Notes.FirstOrDefault(n => n.UEId == u.Id)?.Retenue))
            .ToList());

        return View(inscription);
    }

    public async Task<IActionResult> Create(int? etudiantId)
    {
        var inscription = new Inscription { EtudiantId = etudiantId ?? 0 };

        // Par défaut : l'année en cours, sinon la plus récente
        var aujourdhui = DateTime.Today;
        var annee = await _context.AnneesAcad.Where(a => a.DateDebut <= aujourdhui && a.DateFin >= aujourdhui).FirstOrDefaultAsync()
            ?? await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).FirstOrDefaultAsync();
        inscription.AnneeAcadId = annee?.Id ?? 0;

        await RemplirListesAsync(inscription);
        return View("Formulaire", inscription);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("DateInscription,Statut,EtudiantId,FiliereId,NiveauId,AnneeAcadId")] Inscription inscription)
    {
        await VerifierUniciteAsync(inscription);
        if (ModelState.IsValid)
        {
            _context.Add(inscription);
            if (await EnregistrerAsync())
            {
                Succes("Inscription enregistrée.");
                return RedirectToAction(nameof(Details), new { id = inscription.Id });
            }
        }
        await RemplirListesAsync(inscription);
        return View("Formulaire", inscription);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var inscription = await _context.Inscriptions.FindAsync(id);
        if (inscription == null) return NotFound();

        await RemplirListesAsync(inscription);
        return View("Formulaire", inscription);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id)
    {
        var inscription = await _context.Inscriptions.FindAsync(id);
        if (inscription == null) return NotFound();

        if (await TryUpdateModelAsync(inscription, string.Empty,
                i => i.DateInscription, i => i.Statut, i => i.EtudiantId, i => i.FiliereId, i => i.NiveauId, i => i.AnneeAcadId))
        {
            await VerifierUniciteAsync(inscription);
            if (ModelState.IsValid && await EnregistrerAsync())
            {
                Succes("Inscription mise à jour.");
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        await RemplirListesAsync(inscription);
        return View("Formulaire", inscription);
    }

    // Changement rapide de statut depuis la fiche (Valider / Annuler / Remettre en attente)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Statut(int id, string statut)
    {
        var inscription = await _context.Inscriptions.FindAsync(id);
        if (inscription == null) return NotFound();
        if (!Inscription.Statuts.Contains(statut)) return BadRequest();

        inscription.Statut = statut;
        await _context.SaveChangesAsync();
        Succes($"Statut passé à « {statut} ».");
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var inscription = await ChargerAsync(id.Value);
        if (inscription == null) return NotFound();

        var paiements = inscription.Paiements.Count;
        var notes = inscription.Notes.Count;
        var aides = inscription.Aides.Count;
        var absences = inscription.Absences.Count;
        return ConfirmerSuppression("Supprimer l'inscription",
            $"{inscription.Etudiant?.NomComplet} · {inscription.Filiere?.Code} {inscription.Niveau?.Code} · {inscription.AnneeAcad?.Libelle}", inscription.Id,
            blocages: Si((paiements, $"{Formats.Pluriel(paiements, "paiement enregistré", "paiements enregistrés")} sur cette inscription."),
                (notes, $"{Formats.Pluriel(notes, "note enregistrée", "notes enregistrées")} sur cette inscription."),
                (aides, $"{Formats.Pluriel(aides, "aide accordée", "aides accordées")} sur cette inscription."),
                (absences, $"{Formats.Pluriel(absences, "absence relevée", "absences relevées")} sur cette inscription.")));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var inscription = await _context.Inscriptions.FindAsync(id);
        if (inscription == null) return RedirectToAction(nameof(Index));

        return await SupprimerAsync(inscription, "Inscription supprimée.");
    }

    private Task<Inscription?> ChargerAsync(int id) =>
        _context.Inscriptions
            .Include(i => i.Etudiant)
            .Include(i => i.Filiere)
            .Include(i => i.Niveau)
            .Include(i => i.AnneeAcad)
            .Include(i => i.Paiements)
            .Include(i => i.Notes)
            .Include(i => i.Aides)
            .Include(i => i.Absences).ThenInclude(a => a.Seance).ThenInclude(s => s!.Cours)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id);

    // Un étudiant ne s'inscrit qu'une fois par année académique
    private async Task VerifierUniciteAsync(Inscription inscription)
    {
        var existe = await _context.Inscriptions.AnyAsync(i =>
            i.Id != inscription.Id && i.EtudiantId == inscription.EtudiantId && i.AnneeAcadId == inscription.AnneeAcadId);
        if (existe)
            ModelState.AddModelError(nameof(Inscription.AnneeAcadId), "Cet étudiant est déjà inscrit pour cette année académique.");
    }

    private async Task RemplirListesAsync(Inscription? inscription = null)
    {
        var etudiants = await _context.Etudiants.OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ToListAsync();
        ViewData["EtudiantId"] = etudiants
            .Select(e => new SelectListItem($"{e.NomComplet} — {e.Matricule}", e.Id.ToString(), e.Id == inscription?.EtudiantId))
            .ToList();
        ViewData["FiliereId"] = new SelectList(await _context.Filieres.OrderBy(f => f.Nom).ToListAsync(), nameof(Filiere.Id), nameof(Filiere.Nom), inscription?.FiliereId);
        ViewData["NiveauId"] = new SelectList(await _context.Niveaux.OrderBy(n => n.Id).ToListAsync(), nameof(Niveau.Id), nameof(Niveau.Libelle), inscription?.NiveauId);
        ViewData["AnneeAcadId"] = new SelectList(await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).ToListAsync(), nameof(AnneeAcad.Id), nameof(AnneeAcad.Libelle), inscription?.AnneeAcadId);
        ViewData["Statut"] = new SelectList(Inscription.Statuts, inscription?.Statut);
    }
}
