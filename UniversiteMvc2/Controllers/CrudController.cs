using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Controllers;

// Liste, fiche, création, modification et suppression pour n'importe quelle entité.
// Les vues viennent de Views/Shared/Generique, sauf si le contrôleur a ses propres vues.
// Nouveau module = modèle + DbSet + une ligne dans Navigation + :
//     public class SallesController : CrudController<Salle>
//     {
//         public SallesController(ApplicationDbContext context) : base(context) { }
//     }
public abstract class CrudController<T> : AppController where T : class, new()
{
    protected CrudController(ApplicationDbContext context) : base(context) { }

    private Modeles Modeles => HttpContext.RequestServices.GetRequiredService<Modeles>();

    protected DescriptionModele Description => Modeles.Decrire(typeof(T));

    // --- Points d'extension -------------------------------------------------

    protected virtual Task<T> NouvelleAsync() => Task.FromResult(new T());

    // Mise en forme avant validation (majuscules, espaces…)
    protected virtual void Normaliser(T entite) { }

    // Règles métier : ajouter les erreurs dans ModelState
    protected virtual Task ValiderAsync(T entite) => Task.CompletedTask;

    protected virtual IQueryable<T> Trier(IQueryable<T> requete) => requete.OrderBy(e => EF.Property<int>(e, "Id"));

    // Données supplémentaires à charger pour la liste et la fiche (références de références…)
    protected virtual IQueryable<T> Inclure(IQueryable<T> requete) => requete;

    // Listes déroulantes des clés étrangères ; une liste déjà placée dans ViewData est conservée
    protected virtual async Task RemplirListesAsync(T entite)
    {
        foreach (var champ in Description.Champs.Where(c => c.Cible != null && !ViewData.ContainsKey(c.Nom)))
        {
            var valeur = typeof(T).GetProperty(champ.Nom)!.GetValue(entite);
            var lignes = await ToutAsync(champ.Cible!);
            ViewData[champ.Nom] = lignes
                .Select(o => new SelectListItem(Libelles.Texte(o), Libelles.Id(o)?.ToString(), Equals(Libelles.Id(o), valeur)))
                .OrderBy(i => i.Text, StringComparer.Create(Formats.Fr, true))
                .ToList();
        }
    }

    // --- Actions ------------------------------------------------------------

    [HttpGet]
    public virtual async Task<IActionResult> Index(string? q, int page = 1)
    {
        var requete = Inclure(AvecReferences(_context.Set<T>().AsNoTracking()));
        foreach (var collection in Description.Collections) requete = requete.Include(collection.Nom);

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            requete = requete.Where(Recherche(q));
        }

        ViewData["q"] = q;
        return View(await ListePaginee<T>.CreerAsync(Trier(requete.AsSplitQuery()), page));
    }

    [HttpGet]
    public virtual async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var requete = Inclure(AvecReferences(_context.Set<T>().AsNoTrackingWithIdentityResolution()));
        foreach (var collection in Description.Collections)
        {
            requete = requete.Include(collection.Nom);
            foreach (var reference in Modeles.Decrire(collection.Cible).References.Where(r => r != collection.Inverse))
                requete = requete.Include($"{collection.Nom}.{reference}");
        }

        var entite = await requete.AsSplitQuery().FirstOrDefaultAsync(ParId(id.Value));
        if (entite == null) return NotFound();

        return View(entite);
    }

    [HttpGet]
    public virtual async Task<IActionResult> Create()
    {
        var entite = await NouvelleAsync();
        await RemplirListesAsync(entite);
        return View("Formulaire", entite);
    }

    [HttpPost, ActionName("Create")]
    [ValidateAntiForgeryToken]
    public virtual async Task<IActionResult> CreatePost()
    {
        var entite = await NouvelleAsync();
        if (await AppliquerSaisieAsync(entite))
        {
            _context.Add(entite);
            if (await EnregistrerAsync())
            {
                Succes($"{Libelles.Majuscule(Description.Entite.Le)} a été {Description.Entite.Accord("créé")}.");
                return RedirectToAction(nameof(Details), new { id = Libelles.Id(entite) });
            }
        }
        await RemplirListesAsync(entite);
        return View("Formulaire", entite);
    }

    [HttpGet]
    public virtual async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var entite = await _context.Set<T>().FirstOrDefaultAsync(ParId(id.Value));
        if (entite == null) return NotFound();

        await RemplirListesAsync(entite);
        return View("Formulaire", entite);
    }

    [HttpPost, ActionName("Edit")]
    [ValidateAntiForgeryToken]
    public virtual async Task<IActionResult> EditPost(int id)
    {
        var entite = await _context.Set<T>().FirstOrDefaultAsync(ParId(id));
        if (entite == null) return NotFound();

        if (await AppliquerSaisieAsync(entite) && await EnregistrerAsync())
        {
            Succes("Modifications enregistrées.");
            return RedirectToAction(nameof(Details), new { id });
        }
        await RemplirListesAsync(entite);
        return View("Formulaire", entite);
    }

    [HttpGet]
    public virtual async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var entite = await AvecReferences(_context.Set<T>()).FirstOrDefaultAsync(ParId(id.Value));
        if (entite == null) return NotFound();

        // Conséquences déduites des règles de suppression du modèle EF Core
        var blocages = new List<string>();
        var effets = new List<string>();
        foreach (var collection in Description.Collections)
        {
            var entree = _context.Entry(entite).Collection(collection.Nom);
            await entree.LoadAsync();
            var nombre = (entree.CurrentValue as IEnumerable)?.Cast<object>().Count() ?? 0;
            if (nombre == 0) continue;

            var cible = Modeles.Decrire(collection.Cible).Entite;
            var pluriel = nombre > 1;
            var quoi = Formats.Pluriel(nombre, cible.Singulier, cible.Pluriel);
            var verbe = pluriel ? "seront" : "sera";
            switch (collection.Suppression)
            {
                case DeleteBehavior.Cascade or DeleteBehavior.ClientCascade:
                    effets.Add($"{quoi} {verbe} {cible.Accord("supprimé", pluriel)} avec.");
                    break;
                case DeleteBehavior.SetNull or DeleteBehavior.ClientSetNull:
                    effets.Add($"{quoi} {verbe} {cible.Accord("détaché", pluriel)}.");
                    break;
                default:
                    blocages.Add($"{quoi} y {(pluriel ? "sont" : "est")} {cible.Accord("rattaché", pluriel)}.");
                    break;
            }
        }

        return ConfirmerSuppression($"Supprimer {Description.Entite.Le}", Libelles.Texte(entite), id.Value, blocages, effets);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public virtual async Task<IActionResult> DeleteConfirmed(int id)
    {
        var entite = await _context.Set<T>().FirstOrDefaultAsync(ParId(id));
        if (entite == null) return RedirectToAction(nameof(Index));

        return await SupprimerAsync(entite, $"{Libelles.Majuscule(Description.Entite.Le)} a été {Description.Entite.Accord("supprimé")}.");
    }

    // --- Outils -------------------------------------------------------------

    // Ne lie que les champs du formulaire (protection contre l'overposting)
    protected async Task<bool> AppliquerSaisieAsync(T entite)
    {
        var editables = Description.Champs.Select(c => c.Nom).ToHashSet();
        await TryUpdateModelAsync(entite, string.Empty, m => m.PropertyName != null && editables.Contains(m.PropertyName));
        Normaliser(entite);
        await ValiderAsync(entite);
        return ModelState.IsValid;
    }

    protected IQueryable<TEntite> AvecReferences<TEntite>(IQueryable<TEntite> requete) where TEntite : class
    {
        foreach (var reference in Modeles.Decrire(typeof(TEntite)).References)
            requete = requete.Include(reference);
        return requete;
    }

    private static Expression<Func<T, bool>> ParId(int id) => e => EF.Property<int>(e, "Id") == id;

    // Recherche dans toutes les colonnes texte : e => e.Nom.Contains(q) || e.Code.Contains(q) …
    private Expression<Func<T, bool>> Recherche(string q)
    {
        var parametre = Expression.Parameter(typeof(T), "e");
        var contient = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        Expression? corps = null;

        foreach (var colonne in Description.Colonnes.Where(c => c.Type == typeof(string)))
        {
            var propriete = Expression.Property(parametre, colonne.Nom);
            var test = Expression.AndAlso(
                Expression.NotEqual(propriete, Expression.Constant(null, typeof(string))),
                Expression.Call(propriete, contient, Expression.Constant(q)));
            corps = corps == null ? test : Expression.OrElse(corps, test);
        }

        return Expression.Lambda<Func<T, bool>>(corps ?? Expression.Constant(true), parametre);
    }

    private Task<List<object>> ToutAsync(Type type) =>
        (Task<List<object>>)typeof(CrudController<T>)
            .GetMethod(nameof(ToutGeneriqueAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(type)
            .Invoke(this, null)!;

    private async Task<List<object>> ToutGeneriqueAsync<TCible>() where TCible : class =>
        (await AvecReferences(_context.Set<TCible>().AsNoTracking()).ToListAsync()).Cast<object>().ToList();
}
