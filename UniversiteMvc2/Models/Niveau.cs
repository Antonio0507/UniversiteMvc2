using System.ComponentModel.DataAnnotations;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

[Entite("niveau", "niveaux")]
[Groupe("Niveau", "Le code est mis en majuscules ; les niveaux sont classés dans leur ordre de création.")]
public class Niveau
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(10)]
    [Display(Name = "Code", Order = 1, Prompt = "L1")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    [Display(Name = "Libellé", Order = 2, Prompt = "Licence 1")]
    public string Libelle { get; set; } = string.Empty;

    [Display(Name = "Unités d'enseignement")]
    public ICollection<UE> UEs { get; set; } = new List<UE>();

    public ICollection<Inscription> Inscriptions { get; set; } = new List<Inscription>();
}
