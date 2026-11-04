using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

public class PaiementsController : AppController
{
    public PaiementsController(ApplicationDbContext context) : base(context) { }

    private IQueryable<Paiement> Filtrer(string? q, string? mode, int? anneeId)
    {
        var requete = _context.Paiements.AsNoTracking()
            .Include(p => p.Inscription).ThenInclude(i => i!.Etudiant)
            .Include(p => p.Inscription).ThenInclude(i => i!.Filiere)
            .Include(p => p.Inscription).ThenInclude(i => i!.AnneeAcad)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            requete = requete.Where(p => p.Inscription!.Etudiant!.Nom.Contains(q)
                || p.Inscription.Etudiant.Prenom.Contains(q)
                || p.Inscription.Etudiant.Matricule.Contains(q));
        }
        if (!string.IsNullOrEmpty(mode)) requete = requete.Where(p => p.Mode == mode);
        if (anneeId != null) requete = requete.Where(p => p.Inscription!.AnneeAcadId == anneeId);
        return requete;
    }

    public async Task<IActionResult> Index(string? q, string? mode, int? anneeId, int page = 1)
    {
        var requete = Filtrer(q, mode, anneeId);

        ViewData["q"] = q?.Trim();
        ViewData["Mode"] = new SelectList(Paiement.Modes, mode);
        ViewData["AnneeId"] = new SelectList(await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).ToListAsync(), nameof(AnneeAcad.Id), nameof(AnneeAcad.Libelle), anneeId);
        ViewData["Total"] = await requete.SumAsync(p => (decimal?)p.Montant) ?? 0;

        var tri = requete.OrderByDescending(p => p.DatePaiement).ThenByDescending(p => p.Id);
        return View(await ListePaginee<Paiement>.CreerAsync(tri, page));
    }

    public async Task<IActionResult> Exporter(string? q, string? mode, int? anneeId)
    {
        var paiements = await Filtrer(q, mode, anneeId).OrderByDescending(p => p.DatePaiement).ThenByDescending(p => p.Id).ToListAsync();
        var contenu = Csv.Generer(
            ["Reçu", "Date", "Matricule", "Nom", "Prénom", "Filière", "Année", "Mode", "Montant"],
            paiements.Select(p => new object?[]
            {
                Formats.NumeroRecu(p.Id), p.DatePaiement, p.Inscription?.Etudiant?.Matricule, p.Inscription?.Etudiant?.Nom,
                p.Inscription?.Etudiant?.Prenom, p.Inscription?.Filiere?.Code, p.Inscription?.AnneeAcad?.Libelle, p.Mode, p.Montant
            }));
        return File(contenu, "text/csv", "paiements.csv");
    }

    // Soldes restant dus sur les inscriptions validées d'une année
    public async Task<IActionResult> Impayes(int? anneeId)
    {
        var annee = await AnneeOuCouranteAsync(anneeId);
        ViewData["AnneeId"] = new SelectList(await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).ToListAsync(), nameof(AnneeAcad.Id), nameof(AnneeAcad.Libelle), annee?.Id);
        ViewData["annee"] = annee;
        return View(annee == null ? new List<LigneImpaye>() : await _context.ImpayesAsync(annee.Id));
    }

    public async Task<IActionResult> ExporterImpayes(int? anneeId)
    {
        var annee = await AnneeOuCouranteAsync(anneeId);
        var lignes = annee == null ? new List<LigneImpaye>() : await _context.ImpayesAsync(annee.Id);
        var contenu = Csv.Generer(
            ["Matricule", "Nom", "Prénom", "Filière", "Niveau", "Année", "Dû", "Versé", "Reste"],
            lignes.Select(l => new object?[]
            {
                l.Inscription.Etudiant?.Matricule, l.Inscription.Etudiant?.Nom, l.Inscription.Etudiant?.Prenom,
                l.Inscription.Filiere?.Code, l.Inscription.Niveau?.Code, annee?.Libelle, l.Du, l.Verse, l.Reste
            }));
        return File(contenu, "text/csv", $"impayes-{annee?.Libelle}.csv");
    }

    // La fiche d'un paiement est aussi son reçu imprimable
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        // Inscription → Paiements revient sur ce paiement : il faut la résolution d'identité
        var paiement = await _context.Paiements.AsNoTrackingWithIdentityResolution()
            .Include(p => p.Inscription).ThenInclude(i => i!.Etudiant)
            .Include(p => p.Inscription).ThenInclude(i => i!.Filiere)
            .Include(p => p.Inscription).ThenInclude(i => i!.Niveau)
            .Include(p => p.Inscription).ThenInclude(i => i!.AnneeAcad)
            .Include(p => p.Inscription).ThenInclude(i => i!.Paiements)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id);
        if (paiement == null) return NotFound();

        return View(paiement);
    }

    public async Task<IActionResult> Create(int? inscriptionId)
    {
        var paiement = new Paiement { InscriptionId = inscriptionId ?? 0 };
        await RemplirListesAsync(paiement);
        return View("Formulaire", paiement);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Montant,DatePaiement,Mode,InscriptionId")] Paiement paiement)
    {
        await VerifierInscriptionAsync(paiement);
        await VerifierPlafondAsync(paiement);
        if (ModelState.IsValid)
        {
            _context.Add(paiement);
            if (await EnregistrerAsync())
            {
                Succes($"Paiement de {Formats.Montant(paiement.Montant)} enregistré.");
                return RedirectToAction(nameof(Details), new { id = paiement.Id });
            }
        }
        await RemplirListesAsync(paiement);
        return View("Formulaire", paiement);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var paiement = await _context.Paiements.FindAsync(id);
        if (paiement == null) return NotFound();

        await RemplirListesAsync(paiement);
        return View("Formulaire", paiement);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id)
    {
        var paiement = await _context.Paiements.FindAsync(id);
        if (paiement == null) return NotFound();

        var inscriptionAvant = paiement.InscriptionId;
        if (await TryUpdateModelAsync(paiement, string.Empty, p => p.Montant, p => p.DatePaiement, p => p.Mode, p => p.InscriptionId))
        {
            // Un paiement déjà lié à une inscription annulée peut encore être corrigé
            if (paiement.InscriptionId != inscriptionAvant) await VerifierInscriptionAsync(paiement);
            await VerifierPlafondAsync(paiement);
            if (ModelState.IsValid && await EnregistrerAsync())
            {
                Succes("Paiement mis à jour.");
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        await RemplirListesAsync(paiement);
        return View("Formulaire", paiement);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var paiement = await _context.Paiements
            .Include(p => p.Inscription).ThenInclude(i => i!.Etudiant)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (paiement == null) return NotFound();

        return ConfirmerSuppression("Supprimer le paiement",
            $"{Formats.Montant(paiement.Montant)} du {Formats.Date(paiement.DatePaiement)} — {paiement.Inscription?.Etudiant?.NomComplet}", paiement.Id);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var paiement = await _context.Paiements.FindAsync(id);
        if (paiement == null) return RedirectToAction(nameof(Index));

        return await SupprimerAsync(paiement, "Paiement supprimé.");
    }

    // La liste n'affiche pas les inscriptions annulées ; on le vérifie aussi côté serveur
    private async Task VerifierInscriptionAsync(Paiement paiement)
    {
        if (await _context.Inscriptions.AnyAsync(i => i.Id == paiement.InscriptionId && i.Statut == Inscription.Annulee))
            ModelState.AddModelError(nameof(Paiement.InscriptionId), "Impossible d'enregistrer un paiement sur une inscription annulée.");
    }

    // Un versement ne peut pas dépasser ce qui reste à payer (quand un tarif s'applique)
    private async Task VerifierPlafondAsync(Paiement paiement)
    {
        var inscription = await _context.Inscriptions.AsNoTracking().FirstOrDefaultAsync(i => i.Id == paiement.InscriptionId);
        if (inscription == null) return;

        var du = await _context.DuAsync(inscription);
        if (du == null) return;

        var autres = await _context.Paiements
            .Where(p => p.InscriptionId == paiement.InscriptionId && p.Id != paiement.Id)
            .SumAsync(p => (decimal?)p.Montant) ?? 0;
        var reste = Math.Max(0, du.Value - autres);
        if (paiement.Montant > reste)
            ModelState.AddModelError(nameof(Paiement.Montant), $"Le montant dépasse le reste à payer ({Formats.Montant(reste)}).");
    }

    private async Task<AnneeAcad?> AnneeOuCouranteAsync(int? anneeId)
    {
        if (anneeId != null) return await _context.AnneesAcad.FindAsync(anneeId);
        var aujourdhui = DateTime.Today;
        return await _context.AnneesAcad.Where(a => a.DateDebut <= aujourdhui && a.DateFin >= aujourdhui).FirstOrDefaultAsync()
            ?? await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).FirstOrDefaultAsync();
    }

    private async Task RemplirListesAsync(Paiement paiement)
    {
        // Inscriptions annulées exclues, sauf celle déjà liée au paiement
        var inscriptions = await _context.Inscriptions
            .Include(i => i.Etudiant).Include(i => i.Filiere).Include(i => i.Niveau).Include(i => i.AnneeAcad)
            .Where(i => i.Statut != Inscription.Annulee || i.Id == paiement.InscriptionId)
            .OrderByDescending(i => i.AnneeAcad!.DateDebut).ThenBy(i => i.Etudiant!.Nom)
            .ToListAsync();

        ViewData["InscriptionId"] = inscriptions
            .Select(i => new SelectListItem(
                $"{i.Etudiant?.NomComplet} ({i.Etudiant?.Matricule}) · {i.Filiere?.Code} {i.Niveau?.Code} · {i.AnneeAcad?.Libelle}",
                i.Id.ToString(), i.Id == paiement.InscriptionId))
            .ToList();

        var modes = Paiement.Modes.ToList();
        if (!modes.Contains(paiement.Mode)) modes.Add(paiement.Mode);
        ViewData["Mode"] = new SelectList(modes, paiement.Mode);
    }
}
