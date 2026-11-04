using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>Absence d'un étudiant inscrit à une séance ; relevée par la feuille d'appel (Absences/Appel)</summary>
[Entite("absence", "absences", Feminin = true)]
public class Absence
{
    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Clé étrangère vers la séance.</summary>
    public int SeanceId { get; set; }

    /// <summary>Séance manquée (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(SeanceId))]
    [Display(Name = "Séance")]
    public Seance? Seance { get; set; }

    /// <summary>Clé étrangère vers l'inscription de l'étudiant.</summary>
    public int InscriptionId { get; set; }

    /// <summary>Inscription de l'étudiant absent (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(InscriptionId))]
    [Display(Name = "Étudiant")]
    public Inscription? Inscription { get; set; }

    /// <summary>Absence justifiée (certificat, convocation…) ; elle reste comptée, mais à part.</summary>
    [Display(Name = "Justifiée")]
    public bool Justifiee { get; set; }

    /// <summary>Motif de l'absence, facultatif, 200 caractères au plus.</summary>
    [StringLength(200)]
    [Display(Name = "Motif")]
    public string? Motif { get; set; }
}

/// <summary>Une ligne de la feuille d'appel (Absences/Appel)</summary>
public class SaisieAbsence
{
    /// <summary>Inscription de l'étudiant.</summary>
    public int InscriptionId { get; set; }

    /// <summary>Case « Absent » cochée.</summary>
    public bool Absent { get; set; }

    /// <summary>Case « Justifiée » cochée.</summary>
    public bool Justifiee { get; set; }

    /// <summary>Motif saisi.</summary>
    [StringLength(200, ErrorMessage = "Le motif ne dépasse pas 200 caractères.")]
    public string? Motif { get; set; }
}
