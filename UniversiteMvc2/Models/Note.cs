using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniversiteMvc2.Models;

// Note d'un étudiant inscrit dans une UE, sur 20 ; la meilleure des deux sessions est retenue
public class Note
{
    public const decimal Seuil = 10m;

    [Key]
    public int Id { get; set; }

    public int InscriptionId { get; set; }

    [ForeignKey(nameof(InscriptionId))]
    public Inscription? Inscription { get; set; }

    public int UEId { get; set; }

    [ForeignKey(nameof(UEId))]
    public UE? UE { get; set; }

    [Range(0, 20)]
    [Precision(4, 2)]
    [Display(Name = "Session normale")]
    public decimal? Valeur { get; set; }

    [Range(0, 20)]
    [Precision(4, 2)]
    [Display(Name = "Rattrapage")]
    public decimal? Rattrapage { get; set; }

    [NotMapped]
    public decimal? Retenue => (Valeur, Rattrapage) switch
    {
        (null, null) => null,
        (null, var r) => r,
        (var v, null) => v,
        (var v, var r) => Math.Max(v!.Value, r!.Value)
    };
}

// Décision du jury pour une inscription (Délibérations)
public static class Decisions
{
    public const string Admis = "Admis";
    public const string AdmisCompensation = "Admis par compensation";
    public const string Ajourne = "Ajourné";
    public const string Incomplet = "Incomplet";
    public static readonly string[] Toutes = [Admis, AdmisCompensation, Ajourne, Incomplet];
}

// Résultat d'une inscription : moyenne pondérée par les crédits, calculée sur les UE déjà notées
public sealed record Bilan(decimal? Moyenne, int CreditsValides, int CreditsTotal, int UESansNote)
{
    // Toutes les UE validées : admis. Sinon une moyenne d'au moins 10 compense les UE non validées.
    // Tant qu'une UE n'a pas de note, le jury ne peut pas se prononcer.
    public string Decision =>
        CreditsTotal == 0 || UESansNote > 0 ? Decisions.Incomplet
        : CreditsValides == CreditsTotal ? Decisions.Admis
        : Moyenne >= Note.Seuil ? Decisions.AdmisCompensation
        : Decisions.Ajourne;

    public bool Admis => Decision is Decisions.Admis or Decisions.AdmisCompensation;

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

// Une ligne de la grille de saisie (Notes/Index)
public class SaisieNote
{
    public int InscriptionId { get; set; }

    [Range(0, 20, ErrorMessage = "Les notes vont de 0 à 20.")]
    public decimal? Valeur { get; set; }

    [Range(0, 20, ErrorMessage = "Les notes vont de 0 à 20.")]
    public decimal? Rattrapage { get; set; }
}
