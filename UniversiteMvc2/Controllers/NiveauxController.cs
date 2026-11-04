using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

/// <summary>Module entièrement générique : aucune vue propre, tout vient de Views/Shared/Generique</summary>
public class NiveauxController : CrudController<Niveau>
{
    /// <summary>Crée le contrôleur.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    public NiveauxController(ApplicationDbContext context) : base(context) { }

    /// <summary>Met le code en majuscules.</summary>
    protected override void Normaliser(Niveau niveau) => niveau.Code = niveau.Code.Trim().ToUpperInvariant();

    /// <summary>Pas d'index unique sur Niveaux.Code en base : la règle est vérifiée ici</summary>
    protected override async Task ValiderAsync(Niveau niveau)
    {
        if (await _context.Niveaux.AnyAsync(n => n.Code == niveau.Code && n.Id != niveau.Id))
            ModelState.AddModelError(nameof(Niveau.Code), $"Le code « {niveau.Code} » est déjà utilisé.");
    }

    /// <summary>L1, L2, L3, M1, M2 : ordre de création</summary>
    protected override IQueryable<Niveau> Trier(IQueryable<Niveau> requete) => requete.OrderBy(n => n.Id);
}
