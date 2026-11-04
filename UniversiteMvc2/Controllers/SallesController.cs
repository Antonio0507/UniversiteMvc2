using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

// Module entièrement générique : aucune vue propre, tout vient de Views/Shared/Generique
public class SallesController : CrudController<Salle>
{
    public SallesController(ApplicationDbContext context) : base(context) { }

    protected override void Normaliser(Salle salle) => salle.Code = salle.Code.Trim().ToUpperInvariant();

    protected override async Task ValiderAsync(Salle salle)
    {
        if (await _context.Salles.AnyAsync(s => s.Code == salle.Code && s.Id != salle.Id))
            ModelState.AddModelError(nameof(Salle.Code), $"Le code « {salle.Code} » est déjà utilisé.");
    }

    protected override IQueryable<Salle> Trier(IQueryable<Salle> requete) => requete.OrderBy(s => s.Code);

    protected override async Task RemplirListesAsync(Salle salle)
    {
        var types = Salle.Types.ToList();
        if (!types.Contains(salle.Type)) types.Add(salle.Type);
        ViewData[nameof(Salle.Type)] = new SelectList(types, salle.Type);
        await base.RemplirListesAsync(salle);
    }
}
