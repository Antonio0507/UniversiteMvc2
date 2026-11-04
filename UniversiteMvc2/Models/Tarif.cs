using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

// Frais de scolarité dus pour une année, une filière et un niveau
[Entite("tarif", "tarifs")]
[Groupe("Tarif", "Un seul tarif par année, filière et niveau. Sans tarif, le solde d'une inscription n'est pas calculé.")]
public class Tarif
{
    [Key]
    public int Id { get; set; }

    [Display(Name = "Année académique", Order = 1, GroupName = "Tarif")]
    public int AnneeAcadId { get; set; }

    [ForeignKey(nameof(AnneeAcadId))]
    [Display(Name = "Année académique")]
    public AnneeAcad? AnneeAcad { get; set; }

    [Display(Name = "Filière", Order = 2, GroupName = "Tarif")]
    public int FiliereId { get; set; }

    [ForeignKey(nameof(FiliereId))]
    [Display(Name = "Filière")]
    public Filiere? Filiere { get; set; }

    [Display(Name = "Niveau", Order = 3, GroupName = "Tarif")]
    public int NiveauId { get; set; }

    [ForeignKey(nameof(NiveauId))]
    [Display(Name = "Niveau")]
    public Niveau? Niveau { get; set; }

    [Range(1, 100_000_000)]
    [DataType(DataType.Currency)]
    [Display(Name = "Montant annuel", Order = 4, GroupName = "Tarif", Description = "En " + Formats.Devise + ", sans séparateur.")]
    public decimal Montant { get; set; }

    public override string ToString() => $"{Filiere?.Code} {Niveau?.Code} · {AnneeAcad?.Libelle}";
}

// Situation financière d'une inscription ; Du (aides déduites) est null tant qu'aucun tarif ne s'applique
public sealed record Solde(decimal? Du, decimal Verse)
{
    public decimal? Reste => Du == null ? null : Math.Max(0, Du.Value - Verse);

    // Frais de scolarité après bourses et exonérations, jamais négatifs
    public static decimal Net(decimal tarif, decimal aides) => Math.Max(0, tarif - aides);
}
