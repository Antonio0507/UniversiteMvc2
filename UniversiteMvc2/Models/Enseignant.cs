using System.ComponentModel.DataAnnotations;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>Enseignant : une personne qui assure des cours (table Utilisateurs).</summary>
[Entite("enseignant", "enseignants")]
public class Enseignant : Utilisateur
{
    /// <summary>Grades proposés dans le formulaire.</summary>
    public static readonly string[] Grades = ["Assistant", "Maître-assistant", "Maître de conférences", "Professeur"];

    /// <summary>Grade universitaire, facultatif.</summary>
    [StringLength(50)]
    [Display(Name = "Grade", Order = 5, GroupName = "Statut")]
    public string? Grade { get; set; }

    /// <summary>Spécialité, facultative.</summary>
    [StringLength(100)]
    [Display(Name = "Spécialité", Order = 6, GroupName = "Statut")]
    public string? Specialite { get; set; }

    /// <summary>Cours dont il est titulaire.</summary>
    public ICollection<Cours> Cours { get; set; } = new List<Cours>();
}
