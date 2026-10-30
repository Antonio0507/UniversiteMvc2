using System.ComponentModel.DataAnnotations;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

[Entite("étudiant", "étudiants")]
[Groupe("Identité", "Le matricule est proposé automatiquement ; il reste modifiable.")]
public class Etudiant : Utilisateur
{
    [Required]
    [StringLength(20)]
    [Display(Name = "Matricule", Order = 4, GroupName = "Identité")]
    public string Matricule { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Date de naissance", Order = 3, GroupName = "Identité")]
    public DateTime DateNaissance { get; set; }

    public ICollection<Inscription> Inscriptions { get; set; } = new List<Inscription>();
}
