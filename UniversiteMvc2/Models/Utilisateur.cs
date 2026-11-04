using System.ComponentModel.DataAnnotations;

namespace UniversiteMvc2.Models;

/// <summary>
/// Classe mère : Etudiant et Enseignant sont stockés dans la même table (TPH), le type est déterminé par la colonne
/// discriminante "Role". Order et GroupName pilotent l'ordre et le regroupement des champs dans les vues générées.
/// </summary>
public abstract class Utilisateur
{
    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Nom de famille.</summary>
    [Required]
    [StringLength(50)]
    [Display(Name = "Nom", Order = 1, GroupName = "Identité")]
    public string Nom { get; set; } = string.Empty;

    /// <summary>Prénom.</summary>
    [Required]
    [StringLength(50)]
    [Display(Name = "Prénom", Order = 2, GroupName = "Identité")]
    public string Prenom { get; set; } = string.Empty;

    /// <summary>Adresse e-mail, unique.</summary>
    [Required]
    [EmailAddress]
    [StringLength(100)]
    [Display(Name = "E-mail", Order = 10, GroupName = "Contact")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Téléphone, facultatif.</summary>
    [Phone]
    [StringLength(20)]
    [Display(Name = "Téléphone", Order = 11, GroupName = "Contact", Prompt = "+228 90 00 00 00")]
    public string? Telephone { get; set; }

    /// <summary>Nom puis prénom : « AGBEKO Kossi ».</summary>
    [Display(Name = "Nom complet")]
    public string NomComplet => $"{Nom} {Prenom}";
}
