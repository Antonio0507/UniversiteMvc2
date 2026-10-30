using System.ComponentModel.DataAnnotations;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

[Entite("filière", "filières", Feminin = true)]
[Groupe("Filière", "Le code, court et unique, est mis en majuscules.")]
public class Filiere
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    [Display(Name = "Code", Order = 1, Prompt = "INFO")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Intitulé", Order = 2, Prompt = "Informatique")]
    public string Nom { get; set; } = string.Empty;

    [Display(Name = "Unités d'enseignement")]
    public ICollection<UE> UEs { get; set; } = new List<UE>();

    public ICollection<Inscription> Inscriptions { get; set; } = new List<Inscription>();

    public override string ToString() => Nom;
}
