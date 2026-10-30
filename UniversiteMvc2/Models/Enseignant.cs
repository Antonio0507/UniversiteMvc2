using System.ComponentModel.DataAnnotations;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

[Entite("enseignant", "enseignants")]
public class Enseignant : Utilisateur
{
    public static readonly string[] Grades = ["Assistant", "Maître-assistant", "Maître de conférences", "Professeur"];

    [StringLength(50)]
    [Display(Name = "Grade", Order = 5, GroupName = "Statut")]
    public string? Grade { get; set; }

    [StringLength(100)]
    [Display(Name = "Spécialité", Order = 6, GroupName = "Statut")]
    public string? Specialite { get; set; }

    public ICollection<Cours> Cours { get; set; } = new List<Cours>();
}
