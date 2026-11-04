using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Infrastructure.Pdf;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

// Formulaire de génération des documents PDF (attestation, relevé, liste, reçu, procès-verbal)
public class DocumentsController : AppController
{
    private readonly Etablissement _etablissement;

    public DocumentsController(ApplicationDbContext context, IOptions<Etablissement> etablissement) : base(context)
    {
        _etablissement = etablissement.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index(TypeDocument type = TypeDocument.AttestationInscription,
        int? inscriptionId = null, int? paiementId = null, int? anneeAcadId = null, int? filiereId = null, int? niveauId = null)
    {
        var demande = new DemandeDocument
        {
            Type = type,
            InscriptionId = inscriptionId,
            PaiementId = paiementId,
            AnneeAcadId = anneeAcadId,
            FiliereId = filiereId,
            NiveauId = niveauId,
            Lieu = _etablissement.Ville
        };
        await RemplirListesAsync(demande);
        return View(demande);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(DemandeDocument demande)
    {
        if (ModelState.IsValid && await GenererAsync(demande) is { } pdf) return pdf;

        await RemplirListesAsync(demande);
        return View(demande);
    }

    // Raccourci depuis une fiche (inscription ou paiement) : PDF direct avec les réglages par défaut
    [HttpGet]
    public async Task<IActionResult> Telecharger(TypeDocument type, int id)
    {
        // Type inconnu : le binding le rejette et laisse la valeur par défaut, qui produirait une attestation
        if (!ModelState.IsValid) return NotFound();

        var demande = new DemandeDocument { Type = type, InscriptionId = id, PaiementId = id, Lieu = _etablissement.Ville };
        if (await GenererAsync(demande) is { } pdf) return pdf;

        Erreur(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
        return type == TypeDocument.RecuPaiement
            ? RedirectToAction(nameof(Index), new { type, paiementId = id })
            : RedirectToAction(nameof(Index), new { type, inscriptionId = id });
    }

    // Construit le document demandé ; en cas de problème, ajoute l'erreur au formulaire et renvoie null
    private async Task<IActionResult?> GenererAsync(DemandeDocument demande)
    {
        IDocument? document = null;
        var fichier = "document";

        switch (demande.Type)
        {
            case TypeDocument.AttestationInscription:
            case TypeDocument.RelevePaiements:
            case TypeDocument.ReleveNotes:
                var inscription = await ChargerInscriptionAsync(demande.InscriptionId);
                if (inscription == null)
                {
                    ModelState.AddModelError(nameof(demande.InscriptionId), "Choisissez une inscription.");
                    break;
                }
                if (demande.Type != TypeDocument.RelevePaiements && inscription.Statut != Inscription.Validee)
                {
                    ModelState.AddModelError(nameof(demande.InscriptionId),
                        $"Ce document n'est délivré que pour une inscription validée (statut actuel : « {inscription.Statut} »).");
                    break;
                }
                if (demande.Type == TypeDocument.ReleveNotes)
                {
                    var ues = await _context.UEs.AsNoTracking()
                        .Where(u => u.FiliereId == inscription.FiliereId && u.NiveauId == inscription.NiveauId)
                        .OrderBy(u => u.Code)
                        .ToListAsync();
                    document = new ReleveNotes(_etablissement, demande, inscription, ues);
                    fichier = $"releve-notes-{inscription.Etudiant?.Matricule}-{inscription.AnneeAcad?.Libelle}";
                }
                else if (demande.Type == TypeDocument.AttestationInscription)
                {
                    document = new AttestationInscription(_etablissement, demande, inscription);
                    fichier = $"attestation-{inscription.Etudiant?.Matricule}-{inscription.AnneeAcad?.Libelle}";
                }
                else
                {
                    document = new RelevePaiements(_etablissement, demande, inscription, await _context.DuAsync(inscription));
                    fichier = $"releve-{inscription.Etudiant?.Matricule}-{inscription.AnneeAcad?.Libelle}";
                }
                break;

            case TypeDocument.ListeInscrits:
                var annee = demande.AnneeAcadId == null ? null : await _context.AnneesAcad.FindAsync(demande.AnneeAcadId);
                if (annee == null)
                {
                    ModelState.AddModelError(nameof(demande.AnneeAcadId), "Choisissez une année académique.");
                    break;
                }
                var (inscriptions, criteres) = await ChargerListeAsync(demande, annee);
                if (inscriptions.Count == 0)
                {
                    ModelState.AddModelError(string.Empty, "Aucune inscription ne correspond à ces critères.");
                    break;
                }
                document = new ListeInscrits(_etablissement, demande, inscriptions, criteres);
                fichier = $"liste-inscrits-{annee.Libelle}";
                break;

            case TypeDocument.ProcesVerbal:
                var anneePv = demande.AnneeAcadId == null ? null : await _context.AnneesAcad.FindAsync(demande.AnneeAcadId);
                var filiere = demande.FiliereId == null ? null : await _context.Filieres.FindAsync(demande.FiliereId);
                var niveau = demande.NiveauId == null ? null : await _context.Niveaux.FindAsync(demande.NiveauId);
                if (anneePv == null || filiere == null || niveau == null)
                {
                    ModelState.AddModelError(string.Empty, "Le procès-verbal porte sur une promotion : choisissez l'année, la filière et le niveau.");
                    break;
                }
                var resultats = await _context.CalculerAsync(anneePv.Id, filiere.Id, niveau.Id);
                if (resultats.Count == 0)
                {
                    ModelState.AddModelError(string.Empty, "Aucune inscription validée dans cette promotion.");
                    break;
                }
                document = new ProcesVerbal(_etablissement, demande, anneePv, filiere, niveau, resultats);
                fichier = $"pv-{filiere.Code}-{niveau.Code}-{anneePv.Libelle}";
                break;

            case TypeDocument.RecuPaiement:
                var paiement = demande.PaiementId == null ? null : await _context.Paiements.AsNoTrackingWithIdentityResolution()
                    .Include(p => p.Inscription).ThenInclude(i => i!.Etudiant)
                    .Include(p => p.Inscription).ThenInclude(i => i!.Filiere)
                    .Include(p => p.Inscription).ThenInclude(i => i!.Niveau)
                    .Include(p => p.Inscription).ThenInclude(i => i!.AnneeAcad)
                    .Include(p => p.Inscription).ThenInclude(i => i!.Paiements)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(p => p.Id == demande.PaiementId);
                if (paiement == null)
                {
                    ModelState.AddModelError(nameof(demande.PaiementId), "Choisissez un paiement.");
                    break;
                }
                document = new RecuPaiement(_etablissement, demande, paiement, await _context.DuAsync(paiement.Inscription!));
                fichier = $"recu-{Formats.NumeroRecu(paiement.Id)}";
                break;
        }

        if (document == null) return null;

        // Ouvert dans le navigateur (inline) ; le nom sert à l'enregistrement
        var nom = Regex.Replace(fichier, "[^A-Za-z0-9-]+", "-").Trim('-') + ".pdf";
        Response.Headers.ContentDisposition = $"inline; filename=\"{nom}\"";
        return File(document.GeneratePdf(), "application/pdf");
    }

    private Task<Inscription?> ChargerInscriptionAsync(int? id) =>
        id == null
            ? Task.FromResult<Inscription?>(null)
            : _context.Inscriptions.AsNoTracking()
                .Include(i => i.Etudiant)
                .Include(i => i.Filiere)
                .Include(i => i.Niveau)
                .Include(i => i.AnneeAcad)
                .Include(i => i.Paiements)
                .Include(i => i.Notes)
                .AsSplitQuery()
                .FirstOrDefaultAsync(i => i.Id == id);

    private async Task<(List<Inscription> Inscriptions, string Criteres)> ChargerListeAsync(DemandeDocument demande, AnneeAcad annee)
    {
        var requete = _context.Inscriptions.AsNoTracking()
            .Include(i => i.Etudiant).Include(i => i.Filiere).Include(i => i.Niveau).Include(i => i.Paiements)
            .Where(i => i.AnneeAcadId == annee.Id);
        var criteres = new List<string> { $"Année académique {annee.Libelle}" };

        if (demande.FiliereId != null)
        {
            requete = requete.Where(i => i.FiliereId == demande.FiliereId);
            criteres.Add((await _context.Filieres.FindAsync(demande.FiliereId))?.Nom ?? "");
        }
        if (demande.NiveauId != null)
        {
            requete = requete.Where(i => i.NiveauId == demande.NiveauId);
            criteres.Add((await _context.Niveaux.FindAsync(demande.NiveauId))?.Libelle ?? "");
        }
        if (!string.IsNullOrEmpty(demande.Statut))
        {
            requete = requete.Where(i => i.Statut == demande.Statut);
            criteres.Add($"statut « {demande.Statut} »");
        }

        var inscriptions = await requete.AsSplitQuery()
            .OrderBy(i => i.Filiere!.Code).ThenBy(i => i.NiveauId).ThenBy(i => i.Etudiant!.Nom).ThenBy(i => i.Etudiant!.Prenom)
            .ToListAsync();
        return (inscriptions, string.Join(" · ", criteres.Where(c => c.Length > 0)));
    }

    private async Task RemplirListesAsync(DemandeDocument demande)
    {
        var inscriptions = await _context.Inscriptions.AsNoTracking()
            .Include(i => i.Etudiant).Include(i => i.Filiere).Include(i => i.Niveau).Include(i => i.AnneeAcad)
            .OrderByDescending(i => i.AnneeAcad!.DateDebut).ThenBy(i => i.Etudiant!.Nom)
            .ToListAsync();
        ViewData[nameof(demande.InscriptionId)] = inscriptions
            .Select(i => new SelectListItem($"{i} · {i.Statut}", i.Id.ToString(), i.Id == demande.InscriptionId))
            .ToList();

        var paiements = await _context.Paiements.AsNoTracking()
            .Include(p => p.Inscription).ThenInclude(i => i!.Etudiant)
            .OrderByDescending(p => p.DatePaiement).ThenByDescending(p => p.Id)
            .ToListAsync();
        ViewData[nameof(demande.PaiementId)] = paiements
            .Select(p => new SelectListItem($"{p} — {p.Inscription?.Etudiant?.NomComplet} · {Formats.Date(p.DatePaiement)}", p.Id.ToString(), p.Id == demande.PaiementId))
            .ToList();

        ViewData[nameof(demande.AnneeAcadId)] = new SelectList(await _context.AnneesAcad.OrderByDescending(a => a.DateDebut).ToListAsync(), nameof(AnneeAcad.Id), nameof(AnneeAcad.Libelle), demande.AnneeAcadId);
        ViewData[nameof(demande.FiliereId)] = new SelectList(await _context.Filieres.OrderBy(f => f.Nom).ToListAsync(), nameof(Filiere.Id), nameof(Filiere.Nom), demande.FiliereId);
        ViewData[nameof(demande.NiveauId)] = new SelectList(await _context.Niveaux.OrderBy(n => n.Id).ToListAsync(), nameof(Niveau.Id), nameof(Niveau.Libelle), demande.NiveauId);
        ViewData[nameof(demande.Statut)] = new SelectList(Inscription.Statuts, demande.Statut);
    }
}
