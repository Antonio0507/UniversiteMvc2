using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

[Entite("paiement", "paiements")]
[Groupe("Inscription", "Les inscriptions annulées ne sont pas proposées.")]
public class Paiement
{
    public static readonly string[] Modes = ["Espèces", "Mobile Money", "Virement bancaire", "Chèque"];

    [Key]
    public int Id { get; set; }

    [Range(0.01, 10_000_000)]
    [DataType(DataType.Currency)]
    [Display(Name = "Montant", Order = 2, GroupName = "Versement", Description = "En " + Formats.Devise + ", sans séparateur.")]
    public decimal Montant { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date de paiement", Order = 3, GroupName = "Versement")]
    public DateTime DatePaiement { get; set; } = DateTime.Today;

    [Required]
    [StringLength(30)]
    [Display(Name = "Mode de paiement", Order = 4, GroupName = "Versement")]
    public string Mode { get; set; } = "Espèces";

    [Display(Name = "Inscription", Order = 1, GroupName = "Inscription")]
    public int InscriptionId { get; set; }

    [ForeignKey(nameof(InscriptionId))]
    [Display(Name = "Inscription")]
    public Inscription? Inscription { get; set; }

    public override string ToString() => $"{Formats.NumeroRecu(Id)} · {Formats.Montant(Montant)}";
}
