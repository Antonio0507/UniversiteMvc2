using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;

namespace UniversiteMvc2.Infrastructure;

// Colonne affichable dans une liste ou une fiche : propriété simple, ou référence vers une autre entité
public sealed record Colonne(string Nom, string Libelle, Type Type, ModelMetadata Metadonnees, Type? Reference)
{
    public object? Lire(object element) => element.GetType().GetProperty(Nom)?.GetValue(element);
}

// Champ de saisie d'un formulaire ; Cible = entité visée quand le champ est une clé étrangère
public sealed record ChampSaisie(string Nom, ModelMetadata Metadonnees, Type? Cible, bool Large);

public sealed record GroupeChamps(string Nom, string? Note, IReadOnlyList<ChampSaisie> Champs);

// Collection d'entités liées (ex. Niveau.UEs) ; Inverse = référence retour (UE.Niveau)
public sealed record CollectionLiee(string Nom, string Libelle, Type Cible, DeleteBehavior Suppression, string? Inverse)
{
    public IReadOnlyList<object> Lire(object element) =>
        (element.GetType().GetProperty(Nom)?.GetValue(element) as IEnumerable)?.Cast<object>().ToList() ?? [];
}

public sealed class DescriptionModele
{
    public required Type Type { get; init; }
    public required EntiteAttribute Entite { get; init; }
    public required IReadOnlyList<Colonne> Colonnes { get; init; }
    public required IReadOnlyList<GroupeChamps> Groupes { get; init; }
    public required IReadOnlyList<CollectionLiee> Collections { get; init; }

    public IEnumerable<ChampSaisie> Champs => Groupes.SelectMany(g => g.Champs);
    public IEnumerable<string> References => Colonnes.Where(c => c.Reference != null).Select(c => c.Nom);
    public bool Recherchable => Colonnes.Any(c => c.Type == typeof(string));
}

// Décrit une entité à partir du modèle EF Core et des annotations ([Display], [Required]…).
// C'est la source des vues génériques (Views/Shared/Generique) et de CrudController.
public class Modeles
{
    private static readonly ConcurrentDictionary<Type, DescriptionModele> Cache = new();

    private readonly ApplicationDbContext _context;
    private readonly IModelMetadataProvider _metadonnees;

    public Modeles(ApplicationDbContext context, IModelMetadataProvider metadonnees)
    {
        _context = context;
        _metadonnees = metadonnees;
    }

    public DescriptionModele Decrire(Type type) => Cache.GetOrAdd(type, Construire);

    private DescriptionModele Construire(Type type)
    {
        var entite = _context.Model.FindEntityType(type)
            ?? throw new InvalidOperationException($"{type.Name} n'est pas une entité de ApplicationDbContext.");
        var cle = entite.FindPrimaryKey()?.Properties.Select(p => p.Name).ToHashSet() ?? [];
        var description = EntiteAttribute.De(type);

        var colonnes = new List<(double Ordre, Colonne Colonne)>();
        var champs = new List<(double Ordre, string Groupe, ChampSaisie Champ)>();
        var collections = new List<CollectionLiee>();

        var proprietes = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        for (var index = 0; index < proprietes.Length; index++)
        {
            var propriete = proprietes[index];
            var meta = _metadonnees.GetMetadataForProperty(type, propriete.Name);
            if (cle.Contains(propriete.Name) || (!meta.ShowForDisplay && !meta.ShowForEdit)) continue;

            // Ordre [Display(Order)], puis ordre de déclaration
            var ordre = meta.Order + index / 1000.0;

            var navigation = entite.FindNavigation(propriete.Name);
            if (navigation != null)
            {
                if (navigation.IsCollection)
                {
                    collections.Add(new CollectionLiee(propriete.Name, meta.GetDisplayName(), navigation.TargetEntityType.ClrType,
                        navigation.ForeignKey.DeleteBehavior, navigation.Inverse?.Name));
                }
                else
                {
                    // Une référence prend la place de sa clé étrangère dans l'ordre d'affichage
                    var cleEtrangere = navigation.ForeignKey.Properties[0].Name;
                    var ordreCle = _metadonnees.GetMetadataForProperty(type, cleEtrangere).Order;
                    colonnes.Add((ordreCle + index / 1000.0,
                        new Colonne(propriete.Name, meta.GetDisplayName(), propriete.PropertyType, meta, navigation.TargetEntityType.ClrType)));
                }
                continue;
            }

            var proprieteEf = entite.FindProperty(propriete.Name);
            if (proprieteEf == null) continue;   // propriété calculée (NomComplet…) ou non mappée

            var cible = proprieteEf.GetContainingForeignKeys().FirstOrDefault()?.PrincipalEntityType.ClrType;
            if (propriete.CanWrite && meta.ShowForEdit)
            {
                var groupe = propriete.GetCustomAttribute<DisplayAttribute>()?.GetGroupName() ?? Libelles.Majuscule(description.Singulier);
                var longueur = propriete.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength ?? 0;
                champs.Add((ordre, groupe, new ChampSaisie(propriete.Name, meta, cible, longueur >= 100)));
            }
            if (cible == null)
                colonnes.Add((ordre, new Colonne(propriete.Name, meta.GetDisplayName(), propriete.PropertyType, meta, null)));
        }

        var notes = type.GetCustomAttributes<GroupeAttribute>(true).ToDictionary(g => g.Nom, g => g.Note);
        var groupes = champs
            .OrderBy(c => c.Ordre)
            .GroupBy(c => c.Groupe)
            .Select(g =>
            {
                var liste = g.Select(c => c.Champ).ToList();
                // Seul dans son groupe, un champ prend toute la largeur
                if (liste.Count == 1) liste[0] = liste[0] with { Large = true };
                return new GroupeChamps(g.Key, notes.GetValueOrDefault(g.Key), liste);
            })
            .ToList();

        return new DescriptionModele
        {
            Type = type,
            Entite = description,
            Colonnes = colonnes.OrderBy(c => c.Ordre).Select(c => c.Colonne).ToList(),
            Groupes = groupes,
            Collections = collections
        };
    }
}
