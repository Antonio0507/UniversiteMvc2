using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
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

/// <summary>
/// Espace de l'étudiant connecté : ses inscriptions, frais et paiements, notes, absences, emploi du temps, documents
/// PDF et demande de réinscription. Toutes les données sont filtrées sur son propre dossier.
/// </summary>
[Authorize(Roles = Roles.Etudiant)]
public class EspaceEtudiantController : AppController
{
    private readonly Etablissement _etablissement;

    /// <summary>Crée le contrôleur.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    /// <param name="etablissement">Identité de l'établissement imprimée sur les PDF.</param>
    public EspaceEtudiantController(ApplicationDbContext context, IOptions<Etablissement> etablissement) : base(context)
    {
        _etablissement = etablissement.Value;
    }

    /// <summary>Page « Mon espace » ; par défaut l'inscription la plus récente et la semaine en cours.</summary>
    /// <param name="inscriptionId">Inscription affichée, parmi celles de l'étudiant.</param>
    /// <param name="semaine">Un jour de la semaine d'emploi du temps affichée.</param>
    public async Task<IActionResult> Index(int? inscriptionId, DateTime? semaine)
    {
        var etudiant = await EtudiantConnecteAsync();
        if (etudiant == null) return Forbid();

        var inscriptions = await _context.Inscriptions.AsNoTracking()
            .Include(i => i.Filiere).Include(i => i.Niveau).Include(i => i.AnneeAcad)
            .Where(i => i.EtudiantId == etudiant.Id)
            .OrderByDescending(i => i.AnneeAcad!.DateDebut)
            .ToListAsync();

        var jour = (semaine ?? DateTime.Today).Date;
        var lundi = jour.AddDays(-(((int)jour.DayOfWeek + 6) % 7));
        var annee = await _context.AnneeOuverteAsync();
        var reinscription = annee != null && inscriptions.All(i => i.AnneeAcadId != annee.Id) ? annee : null;
        if (reinscription != null) await RemplirListesAsync();

        var choisie = inscriptions.FirstOrDefault(i => i.Id == inscriptionId) ?? inscriptions.FirstOrDefault();
        if (choisie == null)
            return View(new EspaceEtudiant { Etudiant = etudiant, Inscriptions = inscriptions, Lundi = lundi, Reinscription = reinscription });

        var inscription = (await ChargerAsync(etudiant.Id, choisie.Id))!;
        var tarif = await _context.TarifDeAsync(inscription);
        var ues = await _context.UEs.AsNoTracking()
            .Where(u => u.FiliereId == inscription.FiliereId && u.NiveauId == inscription.NiveauId)
            .OrderBy(u => u.Code)
            .ToListAsync();
        var seances = await _context.Seances.AsNoTracking()
            .Include(s => s.Cours).ThenInclude(c => c!.Enseignant)
            .Include(s => s.Salle)
            .Where(s => s.Cours!.UE!.FiliereId == inscription.FiliereId && s.Cours.UE.NiveauId == inscription.NiveauId
                        && s.Date >= lundi && s.Date < lundi.AddDays(7))
            .OrderBy(s => s.Date).ThenBy(s => s.HeureDebut)
            .ToListAsync();

        return View(new EspaceEtudiant
        {
            Etudiant = etudiant,
            Inscriptions = inscriptions,
            Inscription = inscription,
            Tarif = tarif,
            Solde = new Solde(tarif == null ? null : Solde.Net(tarif.Value, inscription.Aides.Sum(a => a.Montant)), inscription.Paiements.Sum(p => p.Montant)),
            UEs = ues,
            Bilan = Bilan.Calculer(ues.Select(u => (u.Credits, inscription.Notes.FirstOrDefault(n => n.UEId == u.Id)?.Retenue)).ToList()),
            Lundi = lundi,
            Seances = seances,
            Reinscription = reinscription
        });
    }

    /// <summary>
    /// Demande d'inscription pour l'année ouverte, si l'étudiant n'y est pas encore inscrit : l'inscription est
    /// créée « En attente » et validée ensuite par la scolarité.
    /// </summary>
    /// <param name="filiereId">Filière demandée.</param>
    /// <param name="niveauId">Niveau demandé.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reinscription(int filiereId, int niveauId)
    {
        var etudiant = await EtudiantConnecteAsync();
        if (etudiant == null) return Forbid();

        var annee = await _context.AnneeOuverteAsync();
        if (annee == null || await _context.Inscriptions.AnyAsync(i => i.EtudiantId == etudiant.Id && i.AnneeAcadId == annee.Id))
            Erreur("Aucune inscription en ligne n'est possible pour le moment.");
        else if (!await _context.Filieres.AnyAsync(f => f.Id == filiereId) || !await _context.Niveaux.AnyAsync(n => n.Id == niveauId))
            Erreur("Choisissez une filière et un niveau.");
        else
        {
            _context.Add(new Inscription
            {
                EtudiantId = etudiant.Id,
                AnneeAcadId = annee.Id,
                FiliereId = filiereId,
                NiveauId = niveauId,
                DateInscription = DateTime.Today,
                Statut = Inscription.EnAttente
            });
            if (await EnregistrerAsync())
                Succes($"Demande d'inscription pour {annee.Libelle} envoyée : elle est en attente de validation par la scolarité.");
            else
                Erreur("Demande refusée : vous êtes déjà inscrit pour cette année.");
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Télécharge un document de l'étudiant : attestation d'inscription et relevé de notes (inscription validée),
    /// relevé des paiements. Toute autre inscription que les siennes répond 404.
    /// </summary>
    /// <param name="type">Attestation, relevé de notes ou relevé des paiements.</param>
    /// <param name="id">Inscription de l'étudiant.</param>
    [HttpGet]
    public async Task<IActionResult> Document(TypeDocument type, int id)
    {
        if (!ModelState.IsValid || type is not (TypeDocument.AttestationInscription or TypeDocument.ReleveNotes or TypeDocument.RelevePaiements))
            return NotFound();

        var etudiant = await EtudiantConnecteAsync();
        if (etudiant == null) return Forbid();
        var inscription = await ChargerAsync(etudiant.Id, id);
        if (inscription == null) return NotFound();

        if (type != TypeDocument.RelevePaiements && inscription.Statut != Inscription.Validee)
        {
            Erreur("Ce document n'est délivré que pour une inscription validée par la scolarité.");
            return RedirectToAction(nameof(Index), new { inscriptionId = id });
        }

        var demande = new DemandeDocument { Type = type, InscriptionId = id, Lieu = _etablissement.Ville };
        IDocument document;
        string fichier;
        switch (type)
        {
            case TypeDocument.AttestationInscription:
                document = new AttestationInscription(_etablissement, demande, inscription);
                fichier = $"attestation-{etudiant.Matricule}-{inscription.AnneeAcad?.Libelle}";
                break;
            case TypeDocument.ReleveNotes:
                var ues = await _context.UEs.AsNoTracking()
                    .Where(u => u.FiliereId == inscription.FiliereId && u.NiveauId == inscription.NiveauId)
                    .OrderBy(u => u.Code)
                    .ToListAsync();
                document = new ReleveNotes(_etablissement, demande, inscription, ues);
                fichier = $"releve-notes-{etudiant.Matricule}-{inscription.AnneeAcad?.Libelle}";
                break;
            default:
                document = new RelevePaiements(_etablissement, demande, inscription, await _context.DuAsync(inscription));
                fichier = $"releve-{etudiant.Matricule}-{inscription.AnneeAcad?.Libelle}";
                break;
        }

        var nom = Regex.Replace(fichier, "[^A-Za-z0-9-]+", "-").Trim('-') + ".pdf";
        Response.Headers.ContentDisposition = $"inline; filename=\"{nom}\"";
        return File(document.GeneratePdf(), "application/pdf");
    }

    // Dossier lié au compte connecté ; null si le compte n'en a pas
    private Task<Etudiant?> EtudiantConnecteAsync()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var n) ? n : 0;
        return _context.Comptes.AsNoTracking().Where(c => c.Id == id).Select(c => c.Etudiant).FirstOrDefaultAsync();
    }

    // Une inscription, seulement si elle appartient à l'étudiant
    private Task<Inscription?> ChargerAsync(int etudiantId, int inscriptionId) =>
        _context.Inscriptions.AsNoTracking()
            .Include(i => i.Etudiant)
            .Include(i => i.Filiere)
            .Include(i => i.Niveau)
            .Include(i => i.AnneeAcad)
            .Include(i => i.Paiements)
            .Include(i => i.Notes)
            .Include(i => i.Aides)
            .Include(i => i.Absences).ThenInclude(a => a.Seance).ThenInclude(s => s!.Cours)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == inscriptionId && i.EtudiantId == etudiantId);

    private async Task RemplirListesAsync()
    {
        ViewData["FiliereId"] = new SelectList(await _context.Filieres.OrderBy(f => f.Nom).ToListAsync(), nameof(Filiere.Id), nameof(Filiere.Nom));
        ViewData["NiveauId"] = new SelectList(await _context.Niveaux.OrderBy(n => n.Id).ToListAsync(), nameof(Niveau.Id), nameof(Niveau.Libelle));
    }
}
