using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

/// <summary>Module entièrement générique : aucune vue propre, tout vient de Views/Shared/Generique</summary>
public class SallesController : CrudController<Salle>
{
    /// <summary>Crée le contrôleur.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    public SallesController(ApplicationDbContext context) : base(context) { }

    /// <summary>Met le code en majuscules.</summary>
    protected override void Normaliser(Salle salle) => salle.Code = salle.Code.Trim().ToUpperInvariant();

    /// <summary>Refuse un code de salle déjà utilisé.</summary>
    protected override async Task ValiderAsync(Salle salle)
    {
        if (await _context.Salles.AnyAsync(s => s.Code == salle.Code && s.Id != salle.Id))
            ModelState.AddModelError(nameof(Salle.Code), $"Le code « {salle.Code} » est déjà utilisé.");
    }

    /// <summary>Classe les salles par code.</summary>
    protected override IQueryable<Salle> Trier(IQueryable<Salle> requete) => requete.OrderBy(s => s.Code);

    /// <summary>Propose la liste des types de salle, en gardant une valeur hors liste déjà enregistrée.</summary>
    protected override async Task RemplirListesAsync(Salle salle)
    {
        var types = Salle.Types.ToList();
        if (!types.Contains(salle.Type)) types.Add(salle.Type);
        ViewData[nameof(Salle.Type)] = new SelectList(types, salle.Type);
        await base.RemplirListesAsync(salle);
    }
}
