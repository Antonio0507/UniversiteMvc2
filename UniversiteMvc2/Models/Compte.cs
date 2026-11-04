using System.ComponentModel.DataAnnotations;

namespace UniversiteMvc2.Models;

/// <summary>
/// Profils de l'application. L'administrateur a tous les droits ; les autres écrivent uniquement dans leurs modules
/// (voir Navigation) et consultent le reste.
/// </summary>
public static class Roles
{
    /// <summary>Tous les droits, administration comprise.</summary>
    public const string Administrateur = "Administrateur";
    /// <summary>Écrit dans la scolarité, l'enseignement et le calendrier.</summary>
    public const string Scolarite = "Scolarité";
    /// <summary>Écrit dans les paiements, tarifs, bourses et exonérations.</summary>
    public const string Comptabilite = "Comptabilité";
    /// <summary>Lecture seule, sans export CSV.</summary>
    public const string Consultation = "Consultation";

    /// <summary>Les quatre profils, dans l'ordre des listes.</summary>
    public static readonly string[] Tous = [Administrateur, Scolarite, Comptabilite, Consultation];
}

/// <summary>
/// Compte de connexion du personnel. Distinct de Utilisateur, qui décrit les étudiants et les enseignants.
/// </summary>
public class Compte
{
    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Adresse e-mail, unique, en minuscules ; sert d'identifiant de connexion.</summary>
    [Required]
    [EmailAddress]
    [StringLength(100)]
    [Display(Name = "E-mail", Prompt = "prenom.nom@universite.tg", Description = "Sert d'identifiant de connexion.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Nom affiché dans l'en-tête et le journal.</summary>
    [Required]
    [StringLength(100)]
    [Display(Name = "Nom affiché")]
    public string Nom { get; set; } = string.Empty;

    /// <summary>Profil du compte (voir Roles).</summary>
    [Required]
    [StringLength(20)]
    [AllowedValues(Roles.Administrateur, Roles.Scolarite, Roles.Comptabilite, Roles.Consultation, ErrorMessage = "Profil inconnu.")]
    [Display(Name = "Profil")]
    public string Role { get; set; } = Roles.Consultation;

    /// <summary>Un compte désactivé ne peut plus se connecter ; ses sessions sont fermées.</summary>
    [Display(Name = "Compte actif")]
    public bool Actif { get; set; } = true;

    /// <summary>Empreinte du mot de passe (PasswordHasher), jamais le mot de passe lui-même.</summary>
    [Required]
    [StringLength(255)]
    public string MotDePasseHash { get; set; } = string.Empty;

    /// <summary>Le mot de passe est provisoire : il doit être changé avant tout autre accès.</summary>
    public bool DoitChangerMotDePasse { get; set; } = true;

    /// <summary>Échecs de connexion consécutifs ; remis à zéro à chaque connexion réussie.</summary>
    public int EchecsConnexion { get; set; }

    /// <summary>Date (UTC) jusqu'à laquelle le compte est verrouillé, ou null.</summary>
    public DateTime? VerrouilleJusquA { get; set; }

    /// <summary>Date (UTC) de la dernière connexion réussie.</summary>
    public DateTime? DerniereConnexion { get; set; }

    /// <summary>
    /// Change avec le mot de passe, le profil ou l'état du compte : les sessions ouvertes deviennent invalides
    /// </summary>
    [Required]
    [StringLength(36)]
    public string Jeton { get; set; } = Guid.NewGuid().ToString();
}
