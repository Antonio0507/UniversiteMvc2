using System.ComponentModel.DataAnnotations;

namespace UniversiteMvc2.Models;

// Classe mère : Etudiant et Enseignant sont stockés dans la même table (TPH),
// le type est déterminé par la colonne discriminante "Role".
// Order et GroupName pilotent l'ordre et le regroupement des champs dans les vues générées.
public abstract class Utilisateur
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Nom", Order = 1, GroupName = "Identité")]
    public string Nom { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    [Display(Name = "Prénom", Order = 2, GroupName = "Identité")]
    public string Prenom { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    [Display(Name = "E-mail", Order = 10, GroupName = "Contact")]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(20)]
    [Display(Name = "Téléphone", Order = 11, GroupName = "Contact", Prompt = "+228 90 00 00 00")]
    public string? Telephone { get; set; }

    [Display(Name = "Nom complet")]
    public string NomComplet => $"{Nom} {Prenom}";
}
