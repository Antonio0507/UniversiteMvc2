using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

// Absence d'un étudiant inscrit à une séance ; relevée par la feuille d'appel (Absences/Appel)
[Entite("absence", "absences", Feminin = true)]
public class Absence
{
    [Key]
    public int Id { get; set; }

    public int SeanceId { get; set; }

    [ForeignKey(nameof(SeanceId))]
    [Display(Name = "Séance")]
    public Seance? Seance { get; set; }

    public int InscriptionId { get; set; }

    [ForeignKey(nameof(InscriptionId))]
    [Display(Name = "Étudiant")]
    public Inscription? Inscription { get; set; }

    [Display(Name = "Justifiée")]
    public bool Justifiee { get; set; }

    [StringLength(200)]
    [Display(Name = "Motif")]
    public string? Motif { get; set; }
}

// Une ligne de la feuille d'appel (Absences/Appel)
public class SaisieAbsence
{
    public int InscriptionId { get; set; }

    public bool Absent { get; set; }

    public bool Justifiee { get; set; }

    [StringLength(200, ErrorMessage = "Le motif ne dépasse pas 200 caractères.")]
    public string? Motif { get; set; }
}
