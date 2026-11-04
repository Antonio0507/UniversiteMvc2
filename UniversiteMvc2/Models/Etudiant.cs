using System.ComponentModel.DataAnnotations;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>Étudiant : une personne qui s'inscrit chaque année (table Utilisateurs).</summary>
[Entite("étudiant", "étudiants")]
[Groupe("Identité", "Le matricule est proposé automatiquement ; il reste modifiable.")]
public class Etudiant : Utilisateur
{
    /// <summary>Matricule unique, proposé automatiquement (ETU2026001) et modifiable.</summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "Matricule", Order = 4, GroupName = "Identité")]
    public string Matricule { get; set; } = string.Empty;

    /// <summary>Date de naissance, imprimée sur l'attestation et le relevé de notes.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Date de naissance", Order = 3, GroupName = "Identité")]
    public DateTime DateNaissance { get; set; }

    /// <summary>Inscriptions de l'étudiant, une par année au plus.</summary>
    public ICollection<Inscription> Inscriptions { get; set; } = new List<Inscription>();
}
