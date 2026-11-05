using System.ComponentModel.DataAnnotations;

namespace UniversiteMvc2.Models;

/// <summary>
/// Formulaire public d'inscription en ligne : crée le dossier étudiant, une inscription « En attente » pour l'année
/// ouverte et le compte de connexion de l'étudiant.
/// </summary>
public class Candidature
{
    /// <summary>Nom de famille.</summary>
    [Required]
    [StringLength(50)]
    [Display(Name = "Nom")]
    public string Nom { get; set; } = string.Empty;

    /// <summary>Prénom.</summary>
    [Required]
    [StringLength(50)]
    [Display(Name = "Prénom")]
    public string Prenom { get; set; } = string.Empty;

    /// <summary>Date de naissance.</summary>
    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Date de naissance")]
    public DateTime? DateNaissance { get; set; }

    /// <summary>Adresse e-mail : identifiant de connexion.</summary>
    [Required]
    [EmailAddress]
    [StringLength(100)]
    [Display(Name = "E-mail", Prompt = "prenom.nom@exemple.com", Description = "Sert d'identifiant de connexion.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Téléphone, facultatif.</summary>
    [Phone]
    [StringLength(20)]
    [Display(Name = "Téléphone", Prompt = "+228 90 00 00 00")]
    public string? Telephone { get; set; }

    /// <summary>Filière demandée.</summary>
    [Required]
    [Display(Name = "Filière")]
    public int? FiliereId { get; set; }

    /// <summary>Niveau demandé.</summary>
    [Required]
    [Display(Name = "Niveau")]
    public int? NiveauId { get; set; }

    /// <summary>Mot de passe choisi.</summary>
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string MotDePasse { get; set; } = string.Empty;

    /// <summary>Mot de passe, à nouveau.</summary>
    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(MotDePasse), ErrorMessage = "Les deux saisies ne correspondent pas.")]
    [Display(Name = "Confirmation")]
    public string Confirmation { get; set; } = string.Empty;
}

/// <summary>
/// Formulaire public d'activation : un étudiant dont le dossier a été créé par la scolarité se crée un compte, en
/// prouvant son identité par son matricule, sa date de naissance et l'e-mail de son dossier.
/// </summary>
public class Activation
{
    /// <summary>Matricule du dossier.</summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "Matricule", Prompt = "ETU2026001")]
    public string Matricule { get; set; } = string.Empty;

    /// <summary>Date de naissance du dossier.</summary>
    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Date de naissance")]
    public DateTime? DateNaissance { get; set; }

    /// <summary>E-mail du dossier : identifiant de connexion.</summary>
    [Required]
    [EmailAddress]
    [StringLength(100)]
    [Display(Name = "E-mail", Description = "Celui que vous avez donné à la scolarité.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Mot de passe choisi.</summary>
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string MotDePasse { get; set; } = string.Empty;

    /// <summary>Mot de passe, à nouveau.</summary>
    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(MotDePasse), ErrorMessage = "Les deux saisies ne correspondent pas.")]
    [Display(Name = "Confirmation")]
    public string Confirmation { get; set; } = string.Empty;
}
