using System.ComponentModel.DataAnnotations;

namespace UniversiteMvc2.Models;

/// <summary>Trace d'une écriture en base ou d'un événement de sécurité ; alimentée par ApplicationDbContext</summary>
public class JournalAudit
{
    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Date et heure de l'événement, en UTC.</summary>
    public DateTime DateUtc { get; set; } = DateTime.UtcNow;

    /// <summary>E-mail de l'utilisateur, ou « système » hors requête.</summary>
    [Required]
    [StringLength(100)]
    public string Auteur { get; set; } = string.Empty;

    /// <summary>Création, Modification, Suppression, Connexion, Connexion refusée…</summary>
    [Required]
    [StringLength(30)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Nom de la classe de l'entité concernée.</summary>
    [Required]
    [StringLength(50)]
    public string Entite { get; set; } = string.Empty;

    /// <summary>Identifiant de l'entité concernée, s'il y en a un.</summary>
    public int? EntiteId { get; set; }

    /// <summary>
    /// Valeurs créées ou supprimées, ou champs modifiés (« ancien → nouveau ») ; jamais le mot de passe.
    /// </summary>
    [StringLength(2000)]
    public string? Details { get; set; }
}
