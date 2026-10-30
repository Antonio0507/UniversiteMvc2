using System.ComponentModel.DataAnnotations;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

[Entite("année académique", "années académiques", Feminin = true)]
[Groupe("Calendrier", "Les valeurs proposées suivent l'année la plus récente.")]
public class AnneeAcad
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    [Display(Name = "Année académique", Order = 1, GroupName = "Calendrier", Prompt = "2026-2027")]
    public string Libelle { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Date de début", Order = 2, GroupName = "Calendrier")]
    public DateTime DateDebut { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date de fin", Order = 3, GroupName = "Calendrier")]
    public DateTime DateFin { get; set; }

    public ICollection<Inscription> Inscriptions { get; set; } = new List<Inscription>();
}
