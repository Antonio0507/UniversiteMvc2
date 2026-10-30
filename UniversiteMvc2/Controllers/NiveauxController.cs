using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

// Module entièrement générique : aucune vue propre, tout vient de Views/Shared/Generique
public class NiveauxController : CrudController<Niveau>
{
    public NiveauxController(ApplicationDbContext context) : base(context) { }

    protected override void Normaliser(Niveau niveau) => niveau.Code = niveau.Code.Trim().ToUpperInvariant();

    // Pas d'index unique sur Niveaux.Code en base : la règle est vérifiée ici
    protected override async Task ValiderAsync(Niveau niveau)
    {
        if (await _context.Niveaux.AnyAsync(n => n.Code == niveau.Code && n.Id != niveau.Id))
            ModelState.AddModelError(nameof(Niveau.Code), $"Le code « {niveau.Code} » est déjà utilisé.");
    }

    // L1, L2, L3, M1, M2 : ordre de création
    protected override IQueryable<Niveau> Trier(IQueryable<Niveau> requete) => requete.OrderBy(n => n.Id);
}
