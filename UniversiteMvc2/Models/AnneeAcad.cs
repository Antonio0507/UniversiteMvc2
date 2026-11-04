using System.ComponentModel.DataAnnotations;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>Année académique : période qui encadre inscriptions, tarifs, séances et délibérations.</summary>
[Entite("année académique", "années académiques", Feminin = true)]
[Groupe("Calendrier", "Les valeurs proposées suivent l'année la plus récente.")]
public class AnneeAcad
{
    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Libellé unique, par exemple 2026-2027.</summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "Année académique", Order = 1, GroupName = "Calendrier", Prompt = "2026-2027")]
    public string Libelle { get; set; } = string.Empty;

    /// <summary>Premier jour de l'année académique.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Date de début", Order = 2, GroupName = "Calendrier")]
    public DateTime DateDebut { get; set; }

    /// <summary>Dernier jour de l'année académique ; doit suivre la date de début.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Date de fin", Order = 3, GroupName = "Calendrier")]
    public DateTime DateFin { get; set; }

    /// <summary>Inscriptions de l'année.</summary>
    public ICollection<Inscription> Inscriptions { get; set; } = new List<Inscription>();
}
