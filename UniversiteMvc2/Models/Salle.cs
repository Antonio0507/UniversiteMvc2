using System.ComponentModel.DataAnnotations;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>Salle de cours, de TD ou d'examen.</summary>
[Entite("salle", "salles", Feminin = true)]
[Groupe("Salle", "Le code, court et unique, est mis en majuscules.")]
public class Salle
{
    /// <summary>Types de salle proposés dans le formulaire.</summary>
    public static readonly string[] Types = ["Amphithéâtre", "Salle de cours", "Salle de TD", "Laboratoire", "Salle informatique"];

    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Code court et unique, en majuscules (A101).</summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "Code", Order = 1, GroupName = "Salle", Prompt = "A101")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Nom de la salle.</summary>
    [Required]
    [StringLength(100)]
    [Display(Name = "Nom", Order = 2, GroupName = "Salle", Prompt = "Amphithéâtre A")]
    public string Nom { get; set; } = string.Empty;

    /// <summary>Type de salle.</summary>
    [Required]
    [StringLength(30)]
    [Display(Name = "Type", Order = 3, GroupName = "Capacité")]
    public string Type { get; set; } = "Salle de cours";

    /// <summary>Nombre de places, de 1 à 5 000.</summary>
    [Range(1, 5000)]
    [Display(Name = "Places", Order = 4, GroupName = "Capacité")]
    public int Capacite { get; set; } = 40;

    /// <summary>Séances programmées dans la salle.</summary>
    [Display(Name = "Séances")]
    public ICollection<Seance> Seances { get; set; } = new List<Seance>();

    /// <summary>Code et nom : « A101 — Amphithéâtre A ».</summary>
    public override string ToString() => $"{Code} — {Nom}";
}
