using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

// Bourses, exonérations et réductions : vues génériques ; le montant est déduit du solde de l'inscription (Soldes)
public class AidesController : CrudController<Aide>
{
    public AidesController(ApplicationDbContext context) : base(context) { }

    // Depuis la fiche d'une inscription : Aides/Create?inscriptionId=12
    protected override Task<Aide> NouvelleAsync() =>
        Task.FromResult(new Aide { InscriptionId = int.TryParse(Request.Query["inscriptionId"], out var id) ? id : 0 });

    protected override void Normaliser(Aide aide) => aide.Motif = string.IsNullOrWhiteSpace(aide.Motif) ? null : aide.Motif.Trim();

    protected override async Task ValiderAsync(Aide aide)
    {
        var inscription = await _context.Inscriptions.AsNoTracking().FirstOrDefaultAsync(i => i.Id == aide.InscriptionId);
        if (inscription == null) return;

        if (inscription.Statut == Inscription.Annulee)
        {
            ModelState.AddModelError(nameof(Aide.InscriptionId), "Impossible d'accorder une aide sur une inscription annulée.");
            return;
        }

        var tarif = await _context.TarifDeAsync(inscription);
        if (tarif == null) return;

        var autres = await _context.Aides
            .Where(a => a.InscriptionId == aide.InscriptionId && a.Id != aide.Id)
            .SumAsync(a => (decimal?)a.Montant) ?? 0;
        if (autres + aide.Montant > tarif)
            ModelState.AddModelError(nameof(Aide.Montant),
                $"Les aides dépasseraient le tarif de l'inscription ({Formats.Montant(tarif.Value)}, dont {Formats.Montant(autres)} déjà accordés).");
    }

    protected override IQueryable<Aide> Inclure(IQueryable<Aide> requete) =>
        requete.Include(a => a.Inscription).ThenInclude(i => i!.Etudiant)
            .Include(a => a.Inscription).ThenInclude(i => i!.Filiere)
            .Include(a => a.Inscription).ThenInclude(i => i!.Niveau)
            .Include(a => a.Inscription).ThenInclude(i => i!.AnneeAcad);

    protected override IQueryable<Aide> Trier(IQueryable<Aide> requete) =>
        requete.OrderByDescending(a => a.Inscription!.AnneeAcad!.DateDebut).ThenBy(a => a.Inscription!.Etudiant!.Nom);

    protected override async Task RemplirListesAsync(Aide aide)
    {
        // Inscriptions annulées exclues, sauf celle déjà liée à l'aide
        var inscriptions = await _context.Inscriptions.AsNoTracking()
            .Include(i => i.Etudiant).Include(i => i.Filiere).Include(i => i.Niveau).Include(i => i.AnneeAcad)
            .Where(i => i.Statut != Inscription.Annulee || i.Id == aide.InscriptionId)
            .OrderByDescending(i => i.AnneeAcad!.DateDebut).ThenBy(i => i.Etudiant!.Nom)
            .ToListAsync();
        ViewData[nameof(Aide.InscriptionId)] = inscriptions
            .Select(i => new SelectListItem(
                $"{i.Etudiant?.NomComplet} ({i.Etudiant?.Matricule}) · {i.Filiere?.Code} {i.Niveau?.Code} · {i.AnneeAcad?.Libelle}",
                i.Id.ToString(), i.Id == aide.InscriptionId))
            .ToList();

        var types = Aide.Types.ToList();
        if (!types.Contains(aide.Type)) types.Add(aide.Type);
        ViewData[nameof(Aide.Type)] = new SelectList(types, aide.Type);
    }
}
