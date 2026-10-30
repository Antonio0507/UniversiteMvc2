using System.ComponentModel.DataAnnotations;

namespace UniversiteMvc2.Models;

// Profils de l'application. L'administrateur a tous les droits ; les autres écrivent uniquement
// dans leurs modules (voir Navigation) et consultent le reste.
public static class Roles
{
    public const string Administrateur = "Administrateur";
    public const string Scolarite = "Scolarité";
    public const string Comptabilite = "Comptabilité";
    public const string Consultation = "Consultation";

    public static readonly string[] Tous = [Administrateur, Scolarite, Comptabilite, Consultation];
}

// Compte de connexion du personnel. Distinct de Utilisateur, qui décrit les étudiants et les enseignants.
public class Compte
{
    [Key]
    public int Id { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(100)]
    [Display(Name = "E-mail", Prompt = "prenom.nom@universite.tg", Description = "Sert d'identifiant de connexion.")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Nom affiché")]
    public string Nom { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    [AllowedValues(Roles.Administrateur, Roles.Scolarite, Roles.Comptabilite, Roles.Consultation, ErrorMessage = "Profil inconnu.")]
    [Display(Name = "Profil")]
    public string Role { get; set; } = Roles.Consultation;

    [Display(Name = "Compte actif")]
    public bool Actif { get; set; } = true;

    [Required]
    [StringLength(255)]
    public string MotDePasseHash { get; set; } = string.Empty;

    public bool DoitChangerMotDePasse { get; set; } = true;

    public int EchecsConnexion { get; set; }

    public DateTime? VerrouilleJusquA { get; set; }

    public DateTime? DerniereConnexion { get; set; }

    // Change avec le mot de passe, le profil ou l'état du compte : les sessions ouvertes deviennent invalides
    [Required]
    [StringLength(36)]
    public string Jeton { get; set; } = Guid.NewGuid().ToString();
}
