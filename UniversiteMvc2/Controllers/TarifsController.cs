using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

/// <summary>Module entièrement générique : aucune vue propre, tout vient de Views/Shared/Generique</summary>
public class TarifsController : CrudController<Tarif>
{
    /// <summary>Crée le contrôleur.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    public TarifsController(ApplicationDbContext context) : base(context) { }

    /// <summary>Refuse un second tarif pour la même année, filière et niveau.</summary>
    protected override async Task ValiderAsync(Tarif tarif)
    {
        if (await _context.Tarifs.AnyAsync(t => t.Id != tarif.Id && t.AnneeAcadId == tarif.AnneeAcadId
                && t.FiliereId == tarif.FiliereId && t.NiveauId == tarif.NiveauId))
            ModelState.AddModelError(nameof(Tarif.NiveauId), "Un tarif existe déjà pour cette année, cette filière et ce niveau.");
    }

    /// <summary>Classe les tarifs de l'année la plus récente à la plus ancienne, puis par filière et niveau.</summary>
    protected override IQueryable<Tarif> Trier(IQueryable<Tarif> requete) =>
        requete.OrderByDescending(t => t.AnneeAcad!.DateDebut).ThenBy(t => t.Filiere!.Code).ThenBy(t => t.NiveauId);
}
