using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>Frais de scolarité dus pour une année, une filière et un niveau</summary>
[Entite("tarif", "tarifs")]
[Groupe("Tarif", "Un seul tarif par année, filière et niveau. Sans tarif, le solde d'une inscription n'est pas calculé.")]
public class Tarif
{
    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Clé étrangère vers l'année académique.</summary>
    [Display(Name = "Année académique", Order = 1, GroupName = "Tarif")]
    public int AnneeAcadId { get; set; }

    /// <summary>Année académique (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(AnneeAcadId))]
    [Display(Name = "Année académique")]
    public AnneeAcad? AnneeAcad { get; set; }

    /// <summary>Clé étrangère vers la filière.</summary>
    [Display(Name = "Filière", Order = 2, GroupName = "Tarif")]
    public int FiliereId { get; set; }

    /// <summary>Filière (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(FiliereId))]
    [Display(Name = "Filière")]
    public Filiere? Filiere { get; set; }

    /// <summary>Clé étrangère vers le niveau.</summary>
    [Display(Name = "Niveau", Order = 3, GroupName = "Tarif")]
    public int NiveauId { get; set; }

    /// <summary>Niveau (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(NiveauId))]
    [Display(Name = "Niveau")]
    public Niveau? Niveau { get; set; }

    /// <summary>Montant annuel des frais de scolarité, en F CFA.</summary>
    [Range(1, 100_000_000)]
    [DataType(DataType.Currency)]
    [Display(Name = "Montant annuel", Order = 4, GroupName = "Tarif", Description = "En " + Formats.Devise + ", sans séparateur.")]
    public decimal Montant { get; set; }

    /// <summary>Promotion et année : « INFO L1 · 2026-2027 ».</summary>
    public override string ToString() => $"{Filiere?.Code} {Niveau?.Code} · {AnneeAcad?.Libelle}";
}

/// <summary>
/// Situation financière d'une inscription ; Du (aides déduites) est null tant qu'aucun tarif ne s'applique
/// </summary>
/// <param name="Du">Frais dus, aides déduites ; null sans tarif.</param>
/// <param name="Verse">Total des versements.</param>
public sealed record Solde(decimal? Du, decimal Verse)
{
    /// <summary>Reste à payer, jamais négatif ; null sans tarif.</summary>
    public decimal? Reste => Du == null ? null : Math.Max(0, Du.Value - Verse);

    /// <summary>Frais de scolarité après bourses et exonérations, jamais négatifs</summary>
    /// <param name="tarif">Tarif annuel.</param>
    /// <param name="aides">Total des aides accordées.</param>
    public static decimal Net(decimal tarif, decimal aides) => Math.Max(0, tarif - aides);
}
