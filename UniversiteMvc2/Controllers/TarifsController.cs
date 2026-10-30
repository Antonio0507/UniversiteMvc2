using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

// Module entièrement générique : aucune vue propre, tout vient de Views/Shared/Generique
public class TarifsController : CrudController<Tarif>
{
    public TarifsController(ApplicationDbContext context) : base(context) { }

    protected override async Task ValiderAsync(Tarif tarif)
    {
        if (await _context.Tarifs.AnyAsync(t => t.Id != tarif.Id && t.AnneeAcadId == tarif.AnneeAcadId
                && t.FiliereId == tarif.FiliereId && t.NiveauId == tarif.NiveauId))
            ModelState.AddModelError(nameof(Tarif.NiveauId), "Un tarif existe déjà pour cette année, cette filière et ce niveau.");
    }

    protected override IQueryable<Tarif> Trier(IQueryable<Tarif> requete) =>
        requete.OrderByDescending(t => t.AnneeAcad!.DateDebut).ThenBy(t => t.Filiere!.Code).ThenBy(t => t.NiveauId);
}
