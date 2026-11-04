using System.ComponentModel.DataAnnotations;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>Niveau d'études : L1, L2, L3, M1, M2.</summary>
[Entite("niveau", "niveaux")]
[Groupe("Niveau", "Le code est mis en majuscules ; les niveaux sont classés dans leur ordre de création.")]
public class Niveau
{
    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Code court et unique, en majuscules (L1).</summary>
    [Required]
    [StringLength(10)]
    [Display(Name = "Code", Order = 1, Prompt = "L1")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Libellé (Licence 1).</summary>
    [Required]
    [StringLength(50)]
    [Display(Name = "Libellé", Order = 2, Prompt = "Licence 1")]
    public string Libelle { get; set; } = string.Empty;

    /// <summary>UE enseignées à ce niveau.</summary>
    [Display(Name = "Unités d'enseignement")]
    public ICollection<UE> UEs { get; set; } = new List<UE>();

    /// <summary>Inscriptions à ce niveau.</summary>
    public ICollection<Inscription> Inscriptions { get; set; } = new List<Inscription>();
}
