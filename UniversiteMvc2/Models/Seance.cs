using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

// Créneau daté de l'emploi du temps : cours, TD, TP ou épreuve d'examen
[Entite("séance", "séances", Feminin = true)]
[Groupe("Horaire", "Une salle, un enseignant ou une promotion ne peuvent pas avoir deux séances qui se chevauchent.")]
[Groupe("Lieu", "Peut rester vide pour une séance à distance ou une salle à confirmer.")]
public class Seance
{
    public const string Examen = "Examen";
    public static readonly string[] Types = ["Cours magistral", "Travaux dirigés", "Travaux pratiques", Examen, "Rattrapage"];

    [Key]
    public int Id { get; set; }

    [Display(Name = "Cours", Order = 1, GroupName = "Séance")]
    public int CoursId { get; set; }

    [ForeignKey(nameof(CoursId))]
    [Display(Name = "Cours")]
    public Cours? Cours { get; set; }

    [Required]
    [StringLength(30)]
    [Display(Name = "Type", Order = 2, GroupName = "Séance")]
    public string Type { get; set; } = "Cours magistral";

    [DataType(DataType.Date)]
    [Display(Name = "Date", Order = 3, GroupName = "Horaire")]
    public DateTime Date { get; set; } = DateTime.Today;

    [DataType(DataType.Time)]
    [DisplayFormat(DataFormatString = @"{0:hh\:mm}", ApplyFormatInEditMode = true)]
    [Display(Name = "Début", Order = 4, GroupName = "Horaire")]
    public TimeSpan HeureDebut { get; set; } = new(8, 0, 0);

    [DataType(DataType.Time)]
    [DisplayFormat(DataFormatString = @"{0:hh\:mm}", ApplyFormatInEditMode = true)]
    [Display(Name = "Fin", Order = 5, GroupName = "Horaire")]
    public TimeSpan HeureFin { get; set; } = new(10, 0, 0);

    [Display(Name = "Salle", Order = 6, GroupName = "Lieu")]
    public int? SalleId { get; set; }

    [ForeignKey(nameof(SalleId))]
    [Display(Name = "Salle")]
    public Salle? Salle { get; set; }

    [Display(Name = "Absences")]
    public ICollection<Absence> Absences { get; set; } = new List<Absence>();

    // Durée en heures, comptée dans les heures d'absence
    [NotMapped]
    public decimal Duree => (decimal)(HeureFin - HeureDebut).TotalHours;

    public bool Chevauche(Seance autre) =>
        Date.Date == autre.Date.Date && HeureDebut < autre.HeureFin && autre.HeureDebut < HeureFin;

    public override string ToString() =>
        $"{Cours?.Intitule} · {Formats.Date(Date)} {Formats.Heure(HeureDebut)}–{Formats.Heure(HeureFin)}";
}
