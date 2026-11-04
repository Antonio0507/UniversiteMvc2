using System.ComponentModel.DataAnnotations;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

[Entite("salle", "salles", Feminin = true)]
[Groupe("Salle", "Le code, court et unique, est mis en majuscules.")]
public class Salle
{
    public static readonly string[] Types = ["Amphithéâtre", "Salle de cours", "Salle de TD", "Laboratoire", "Salle informatique"];

    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    [Display(Name = "Code", Order = 1, GroupName = "Salle", Prompt = "A101")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Nom", Order = 2, GroupName = "Salle", Prompt = "Amphithéâtre A")]
    public string Nom { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    [Display(Name = "Type", Order = 3, GroupName = "Capacité")]
    public string Type { get; set; } = "Salle de cours";

    [Range(1, 5000)]
    [Display(Name = "Places", Order = 4, GroupName = "Capacité")]
    public int Capacite { get; set; } = 40;

    [Display(Name = "Séances")]
    public ICollection<Seance> Seances { get; set; } = new List<Seance>();

    public override string ToString() => $"{Code} — {Nom}";
}
