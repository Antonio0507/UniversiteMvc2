using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>Créneau daté de l'emploi du temps : cours, TD, TP ou épreuve d'examen</summary>
[Entite("séance", "séances", Feminin = true)]
[Groupe("Horaire", "Une salle, un enseignant ou une promotion ne peuvent pas avoir deux séances qui se chevauchent.")]
[Groupe("Lieu", "Peut rester vide pour une séance à distance ou une salle à confirmer.")]
public class Seance
{
    /// <summary>Type de séance mis en évidence dans l'emploi du temps.</summary>
    public const string Examen = "Examen";
    /// <summary>Types de séance proposés dans le formulaire.</summary>
    public static readonly string[] Types = ["Cours magistral", "Travaux dirigés", "Travaux pratiques", Examen, "Rattrapage"];

    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Clé étrangère vers le cours.</summary>
    [Display(Name = "Cours", Order = 1, GroupName = "Séance")]
    public int CoursId { get; set; }

    /// <summary>Cours (et, par lui, l'UE, la promotion et l'enseignant) (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(CoursId))]
    [Display(Name = "Cours")]
    public Cours? Cours { get; set; }

    /// <summary>Type de séance.</summary>
    [Required]
    [StringLength(30)]
    [Display(Name = "Type", Order = 2, GroupName = "Séance")]
    public string Type { get; set; } = "Cours magistral";

    /// <summary>Jour de la séance.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Date", Order = 3, GroupName = "Horaire")]
    public DateTime Date { get; set; } = DateTime.Today;

    /// <summary>Heure de début.</summary>
    [DataType(DataType.Time)]
    [DisplayFormat(DataFormatString = @"{0:hh\:mm}", ApplyFormatInEditMode = true)]
    [Display(Name = "Début", Order = 4, GroupName = "Horaire")]
    public TimeSpan HeureDebut { get; set; } = new(8, 0, 0);

    /// <summary>Heure de fin ; doit suivre le début.</summary>
    [DataType(DataType.Time)]
    [DisplayFormat(DataFormatString = @"{0:hh\:mm}", ApplyFormatInEditMode = true)]
    [Display(Name = "Fin", Order = 5, GroupName = "Horaire")]
    public TimeSpan HeureFin { get; set; } = new(10, 0, 0);

    /// <summary>Clé étrangère vers la salle ; null pour une séance à distance ou une salle à confirmer.</summary>
    [Display(Name = "Salle", Order = 6, GroupName = "Lieu")]
    public int? SalleId { get; set; }

    /// <summary>Salle (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(SalleId))]
    [Display(Name = "Salle")]
    public Salle? Salle { get; set; }

    /// <summary>Absences relevées par la feuille d'appel.</summary>
    [Display(Name = "Absences")]
    public ICollection<Absence> Absences { get; set; } = new List<Absence>();

    /// <summary>Durée en heures, comptée dans les heures d'absence</summary>
    [NotMapped]
    public decimal Duree => (decimal)(HeureFin - HeureDebut).TotalHours;

    /// <summary>
    /// Indique si deux séances se chevauchent : même jour, et l'une commence avant la fin de l'autre. Deux séances
    /// bout à bout ne se chevauchent pas.
    /// </summary>
    /// <param name="autre">Autre séance.</param>
    public bool Chevauche(Seance autre) =>
        Date.Date == autre.Date.Date && HeureDebut < autre.HeureFin && autre.HeureDebut < HeureFin;

    /// <summary>Cours, date et horaire : « Algorithmique · 02/11/2026 08h00–10h00 ».</summary>
    public override string ToString() =>
        $"{Cours?.Intitule} · {Formats.Date(Date)} {Formats.Heure(HeureDebut)}–{Formats.Heure(HeureFin)}";
}
