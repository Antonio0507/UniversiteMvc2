using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniversiteMvc2.Models;

/// <summary>Note d'un étudiant inscrit dans une UE, sur 20 ; la meilleure des deux sessions est retenue</summary>
public class Note
{
    /// <summary>Note minimale qui valide une UE : 10/20.</summary>
    public const decimal Seuil = 10m;

    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Clé étrangère vers l'inscription.</summary>
    public int InscriptionId { get; set; }

    /// <summary>Inscription de l'étudiant (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(InscriptionId))]
    public Inscription? Inscription { get; set; }

    /// <summary>Clé étrangère vers l'UE.</summary>
    public int UEId { get; set; }

    /// <summary>UE notée (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(UEId))]
    public UE? UE { get; set; }

    /// <summary>Note de session normale, de 0 à 20.</summary>
    [Range(0, 20)]
    [Precision(4, 2)]
    [Display(Name = "Session normale")]
    public decimal? Valeur { get; set; }

    /// <summary>Note de rattrapage, de 0 à 20.</summary>
    [Range(0, 20)]
    [Precision(4, 2)]
    [Display(Name = "Rattrapage")]
    public decimal? Rattrapage { get; set; }

    /// <summary>Note retenue : la meilleure des deux sessions, ou null sans note.</summary>
    [NotMapped]
    public decimal? Retenue => (Valeur, Rattrapage) switch
    {
        (null, null) => null,
        (null, var r) => r,
        (var v, null) => v,
        (var v, var r) => Math.Max(v!.Value, r!.Value)
    };
}

/// <summary>Décision du jury pour une inscription (Délibérations)</summary>
public static class Decisions
{
    /// <summary>Toutes les UE validées.</summary>
    public const string Admis = "Admis";
    /// <summary>Pas toutes les UE validées, mais moyenne d'au moins 10.</summary>
    public const string AdmisCompensation = "Admis par compensation";
    /// <summary>Pas toutes les UE validées, et moyenne inférieure à 10.</summary>
    public const string Ajourne = "Ajourné";
    /// <summary>Au moins une UE sans note : le jury ne peut pas se prononcer.</summary>
    public const string Incomplet = "Incomplet";
    /// <summary>Les quatre décisions, dans l'ordre des listes.</summary>
    public static readonly string[] Toutes = [Admis, AdmisCompensation, Ajourne, Incomplet];
}

/// <summary>Résultat d'une inscription : moyenne pondérée par les crédits, calculée sur les UE déjà notées</summary>
/// <param name="Moyenne">Moyenne pondérée par les crédits, arrondie à 2 décimales ; null sans aucune note.</param>
/// <param name="CreditsValides">Crédits des UE dont la note retenue est d'au moins 10.</param>
/// <param name="CreditsTotal">Crédits de toutes les UE du cursus.</param>
/// <param name="UESansNote">Nombre d'UE du cursus encore sans note.</param>
public sealed record Bilan(decimal? Moyenne, int CreditsValides, int CreditsTotal, int UESansNote)
{
    /// <summary>
    /// Toutes les UE validées : admis. Sinon une moyenne d'au moins 10 compense les UE non validées. Tant qu'une UE
    /// n'a pas de note, le jury ne peut pas se prononcer.
    /// </summary>
    public string Decision =>
        CreditsTotal == 0 || UESansNote > 0 ? Decisions.Incomplet
        : CreditsValides == CreditsTotal ? Decisions.Admis
        : Moyenne >= Note.Seuil ? Decisions.AdmisCompensation
        : Decisions.Ajourne;

    /// <summary>Admis, avec ou sans compensation.</summary>
    public bool Admis => Decision is Decisions.Admis or Decisions.AdmisCompensation;

    /// <summary>Calcule le bilan à partir des UE du cursus et de leur note retenue.</summary>
    /// <param name="lignes">Une ligne par UE : ses crédits et sa note retenue (null si pas de note).</param>
    public static Bilan Calculer(IReadOnlyCollection<(int Credits, decimal? Retenue)> lignes)
    {
        var notees = lignes.Where(l => l.Retenue != null).ToList();
        var credits = notees.Sum(l => l.Credits);
        decimal? moyenne = credits == 0
            ? null
            : Math.Round(notees.Sum(l => l.Credits * l.Retenue!.Value) / credits, 2, MidpointRounding.AwayFromZero);

        return new Bilan(
            moyenne,
            notees.Where(l => l.Retenue >= Note.Seuil).Sum(l => l.Credits),
            lignes.Sum(l => l.Credits),
            lignes.Count - notees.Count);
    }
}

/// <summary>Une ligne de la grille de saisie (Notes/Index)</summary>
public class SaisieNote
{
    /// <summary>Inscription de l'étudiant.</summary>
    public int InscriptionId { get; set; }

    /// <summary>Note de session normale saisie.</summary>
    [Range(0, 20, ErrorMessage = "Les notes vont de 0 à 20.")]
    public decimal? Valeur { get; set; }

    /// <summary>Note de rattrapage saisie.</summary>
    [Range(0, 20, ErrorMessage = "Les notes vont de 0 à 20.")]
    public decimal? Rattrapage { get; set; }
}
