using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Controllers;

/// <summary>
/// Liste, fiche, création, modification et suppression pour n'importe quelle entité. Les vues viennent de
/// Views/Shared/Generique, sauf si le contrôleur a ses propres vues. Nouveau module = modèle + DbSet + une ligne dans
/// Navigation + une classe qui hérite de CrudController&lt;T&gt;.
/// </summary>
public abstract class CrudController<T> : AppController where T : class, new()
{
    /// <summary>Initialise le contrôleur générique.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    protected CrudController(ApplicationDbContext context) : base(context) { }

    private Modeles Modeles => HttpContext.RequestServices.GetRequiredService<Modeles>();

    /// <summary>Description de l'entité gérée : colonnes, groupes de champs, collections liées.</summary>
    protected DescriptionModele Description => Modeles.Decrire(typeof(T));

    // --- Points d'extension -------------------------------------------------

    /// <summary>Crée l'élément de départ d'un formulaire de création (valeurs par défaut).</summary>
    /// <returns>Le nouvel élément, non enregistré.</returns>
    protected virtual Task<T> NouvelleAsync() => Task.FromResult(new T());

    /// <summary>Mise en forme avant validation (majuscules, espaces…)</summary>
    /// <param name="entite">Élément saisi.</param>
    protected virtual void Normaliser(T entite) { }

    /// <summary>Règles métier : ajouter les erreurs dans ModelState</summary>
    /// <param name="entite">Élément saisi, déjà normalisé.</param>
    protected virtual Task ValiderAsync(T entite) => Task.CompletedTask;

    /// <summary>Ordre de la liste ; par défaut, l'ordre de création.</summary>
    /// <param name="requete">Requête à trier.</param>
    protected virtual IQueryable<T> Trier(IQueryable<T> requete) => requete.OrderBy(e => EF.Property<int>(e, "Id"));

    /// <summary>Données supplémentaires à charger pour la liste et la fiche (références de références…)</summary>
    /// <param name="requete">Requête de la liste ou de la fiche.</param>
    protected virtual IQueryable<T> Inclure(IQueryable<T> requete) => requete;

    /// <summary>Listes déroulantes des clés étrangères ; une liste déjà placée dans ViewData est conservée</summary>
    /// <param name="entite">Élément affiché dans le formulaire (sert à pré-sélectionner les valeurs).</param>
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

    /// <summary>Liste paginée, avec recherche dans toutes les colonnes texte.</summary>
    /// <param name="q">Texte recherché, facultatif.</param>
    /// <param name="page">Numéro de page, à partir de 1.</param>
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

    /// <summary>Affiche la fiche d'un élément, avec ses collections liées.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    /// <returns>La fiche, ou 404 si l'élément n'existe pas.</returns>
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

    /// <summary>Affiche le formulaire de création.</summary>
    [HttpGet]
    public virtual async Task<IActionResult> Create()
    {
        var entite = await NouvelleAsync();
        await RemplirListesAsync(entite);
        return View("Formulaire", entite);
    }

    /// <summary>Enregistre un nouvel élément, puis ouvre sa fiche ; en cas d'erreur, réaffiche le formulaire.</summary>
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

    /// <summary>Affiche le formulaire de modification d'un élément.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    [HttpGet]
    public virtual async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var entite = await _context.Set<T>().FirstOrDefaultAsync(ParId(id.Value));
        if (entite == null) return NotFound();

        await RemplirListesAsync(entite);
        return View("Formulaire", entite);
    }

    /// <summary>
    /// Enregistre les modifications d'un élément, puis revient sur sa fiche ; en cas d'erreur, réaffiche le
    /// formulaire.
    /// </summary>
    /// <param name="id">Identifiant de l'élément.</param>
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

    /// <summary>
    /// Affiche la confirmation de suppression. Ce qui bloque ou ce qui sera supprimé avec est déduit des règles de
    /// suppression du modèle EF Core.
    /// </summary>
    /// <param name="id">Identifiant de l'élément.</param>
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

    /// <summary>Supprime l'élément après confirmation, puis revient à la liste.</summary>
    /// <param name="id">Identifiant de l'élément.</param>
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public virtual async Task<IActionResult> DeleteConfirmed(int id)
    {
        var entite = await _context.Set<T>().FirstOrDefaultAsync(ParId(id));
        if (entite == null) return RedirectToAction(nameof(Index));

        return await SupprimerAsync(entite, $"{Libelles.Majuscule(Description.Entite.Le)} a été {Description.Entite.Accord("supprimé")}.");
    }

    // --- Outils -------------------------------------------------------------

    /// <summary>Ne lie que les champs du formulaire (protection contre l'overposting)</summary>
    /// <param name="entite">Élément à remplir.</param>
    /// <returns>true si la saisie est valide, règles métier comprises.</returns>
    protected async Task<bool> AppliquerSaisieAsync(T entite)
    {
        var editables = Description.Champs.Select(c => c.Nom).ToHashSet();
        await TryUpdateModelAsync(entite, string.Empty, m => m.PropertyName != null && editables.Contains(m.PropertyName));
        Normaliser(entite);
        await ValiderAsync(entite);
        return ModelState.IsValid;
    }

    /// <summary>
    /// Ajoute à la requête les références directes de l'entité (clés étrangères), pour les afficher par leur nom.
    /// </summary>
    /// <param name="requete">Requête à compléter.</param>
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
