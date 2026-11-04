using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>
/// Bourse, exonération ou réduction accordée sur une inscription : elle est déduite des frais de scolarité dus
/// </summary>
[Entite("aide", "aides", Feminin = true)]
[Groupe("Bénéficiaire", "Les inscriptions annulées ne sont pas proposées.")]
[Groupe("Aide", "Le total des aides d'une inscription ne peut pas dépasser son tarif.")]
public class Aide
{
    /// <summary>Types d'aide proposés dans le formulaire.</summary>
    public static readonly string[] Types = ["Bourse d'excellence", "Bourse sociale", "Exonération", "Réduction", "Prise en charge"];

    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Clé étrangère vers l'inscription bénéficiaire.</summary>
    [Display(Name = "Inscription", Order = 1, GroupName = "Bénéficiaire")]
    public int InscriptionId { get; set; }

    /// <summary>Inscription bénéficiaire (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(InscriptionId))]
    [Display(Name = "Inscription")]
    public Inscription? Inscription { get; set; }

    /// <summary>Type d'aide (bourse, exonération, réduction…).</summary>
    [Required]
    [StringLength(30)]
    [Display(Name = "Type", Order = 2, GroupName = "Aide")]
    public string Type { get; set; } = "Bourse sociale";

    /// <summary>Montant déduit des frais de scolarité, en F CFA.</summary>
    [Range(1, 100_000_000)]
    [DataType(DataType.Currency)]
    [Display(Name = "Montant", Order = 3, GroupName = "Aide", Description = "En " + Formats.Devise + ", sans séparateur.")]
    public decimal Montant { get; set; }

    /// <summary>Motif ou référence de la décision, facultatif.</summary>
    [StringLength(200)]
    [Display(Name = "Motif", Order = 4, GroupName = "Aide", Prompt = "Décision n° 12/2026 du conseil")]
    public string? Motif { get; set; }

    /// <summary>Type et montant : « Bourse sociale · 100 000 F CFA ».</summary>
    public override string ToString() => $"{Type} · {Formats.Montant(Montant)}";
}
