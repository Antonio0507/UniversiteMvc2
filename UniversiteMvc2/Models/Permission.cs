using System.ComponentModel.DataAnnotations;

namespace UniversiteMvc2.Models;

/// <summary>Niveau d'accès d'un profil à une section.</summary>
public enum Acces
{
    /// <summary>Section invisible et inaccessible.</summary>
    [Display(Name = "Aucun accès")]
    Aucun = 0,

    /// <summary>Consultation seule, sans création, modification, suppression ni export.</summary>
    [Display(Name = "Lecture")]
    Lecture = 1,

    /// <summary>Consultation, création, modification, suppression et export.</summary>
    [Display(Name = "Écriture")]
    Ecriture = 2
}

/// <summary>
/// Droit d'un profil du personnel sur une section, réglé par l'administrateur (page Permissions). Sans ligne, la
/// valeur par défaut s'applique (voir MatriceDroits.ParDefaut).
/// </summary>
public class Permission
{
    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Profil concerné (voir Roles.Configurables).</summary>
    [Required]
    [StringLength(20)]
    public string Role { get; set; } = string.Empty;

    /// <summary>Section concernée : nom du contrôleur (voir Navigation).</summary>
    [Required]
    [StringLength(30)]
    public string Section { get; set; } = string.Empty;

    /// <summary>Niveau d'accès accordé.</summary>
    public Acces Acces { get; set; }
}
