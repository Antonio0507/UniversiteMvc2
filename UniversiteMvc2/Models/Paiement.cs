using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>Versement d'un étudiant sur une inscription ; sa fiche sert de reçu.</summary>
[Entite("paiement", "paiements")]
[Groupe("Inscription", "Les inscriptions annulées ne sont pas proposées.")]
public class Paiement
{
    /// <summary>Modes de paiement proposés dans le formulaire.</summary>
    public static readonly string[] Modes = ["Espèces", "Mobile Money", "Virement bancaire", "Chèque"];

    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Montant versé, en F CFA ; ne peut pas dépasser le reste à payer.</summary>
    [Range(0.01, 10_000_000)]
    [DataType(DataType.Currency)]
    [Display(Name = "Montant", Order = 2, GroupName = "Versement", Description = "En " + Formats.Devise + ", sans séparateur.")]
    public decimal Montant { get; set; }

    /// <summary>Date du versement.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Date de paiement", Order = 3, GroupName = "Versement")]
    public DateTime DatePaiement { get; set; } = DateTime.Today;

    /// <summary>Mode de paiement.</summary>
    [Required]
    [StringLength(30)]
    [Display(Name = "Mode de paiement", Order = 4, GroupName = "Versement")]
    public string Mode { get; set; } = "Espèces";

    /// <summary>Clé étrangère vers l'inscription.</summary>
    [Display(Name = "Inscription", Order = 1, GroupName = "Inscription")]
    public int InscriptionId { get; set; }

    /// <summary>Inscription concernée (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(InscriptionId))]
    [Display(Name = "Inscription")]
    public Inscription? Inscription { get; set; }

    /// <summary>Numéro de reçu et montant : « R-000042 · 150 000 F CFA ».</summary>
    public override string ToString() => $"{Formats.NumeroRecu(Id)} · {Formats.Montant(Montant)}";
}
