using System.ComponentModel.DataAnnotations;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>Filière : domaine d'études (Informatique, Gestion, Droit…).</summary>
[Entite("filière", "filières", Feminin = true)]
[Groupe("Filière", "Le code, court et unique, est mis en majuscules.")]
public class Filiere
{
    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Code court et unique, en majuscules (INFO).</summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "Code", Order = 1, Prompt = "INFO")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Intitulé (Informatique).</summary>
    [Required]
    [StringLength(100)]
    [Display(Name = "Intitulé", Order = 2, Prompt = "Informatique")]
    public string Nom { get; set; } = string.Empty;

    /// <summary>UE enseignées dans la filière.</summary>
    [Display(Name = "Unités d'enseignement")]
    public ICollection<UE> UEs { get; set; } = new List<UE>();

    /// <summary>Inscriptions dans la filière.</summary>
    public ICollection<Inscription> Inscriptions { get; set; } = new List<Inscription>();

    /// <summary>L'intitulé de la filière.</summary>
    public override string ToString() => Nom;
}
