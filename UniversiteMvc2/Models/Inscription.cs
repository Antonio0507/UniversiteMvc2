using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>
/// Inscription : un étudiant, pour une année, dans une filière et un niveau. Pivot des paiements, notes, aides et
/// absences.
/// </summary>
[Entite("inscription", "inscriptions", Feminin = true)]
[Groupe("Cursus", "Un étudiant ne peut être inscrit qu'une fois par année académique.")]
public class Inscription
{
    /// <summary>Statut par défaut d'une nouvelle inscription.</summary>
    public const string EnAttente = "En attente";
    /// <summary>
    /// Inscription confirmée : seule à compter pour les notes, l'appel, les délibérations, les impayés et les
    /// attestations.
    /// </summary>
    public const string Validee = "Validée";
    /// <summary>Inscription annulée : plus de paiement ni d'aide possible.</summary>
    public const string Annulee = "Annulée";
    /// <summary>Les trois statuts, dans l'ordre des listes.</summary>
    public static readonly string[] Statuts = [EnAttente, Validee, Annulee];

    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Date de l'inscription.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Date d'inscription", Order = 5, GroupName = "Dossier")]
    public DateTime DateInscription { get; set; } = DateTime.Today;

    /// <summary>Statut : En attente, Validée ou Annulée.</summary>
    [Required]
    [StringLength(20)]
    [AllowedValues(EnAttente, Validee, Annulee, ErrorMessage = "Statut inconnu.")]
    [Display(Name = "Statut", Order = 6, GroupName = "Dossier")]
    public string Statut { get; set; } = EnAttente;

    /// <summary>Clé étrangère vers l'étudiant.</summary>
    [Display(Name = "Étudiant", Order = 1, GroupName = "Étudiant", Description = "Absent de la liste ? Créez d'abord son dossier dans Étudiants.")]
    public int EtudiantId { get; set; }

    /// <summary>Étudiant inscrit (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(EtudiantId))]
    [Display(Name = "Étudiant")]
    public Etudiant? Etudiant { get; set; }

    /// <summary>Clé étrangère vers la filière.</summary>
    [Display(Name = "Filière", Order = 3, GroupName = "Cursus")]
    public int FiliereId { get; set; }

    /// <summary>Filière (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(FiliereId))]
    [Display(Name = "Filière")]
    public Filiere? Filiere { get; set; }

    /// <summary>Clé étrangère vers le niveau.</summary>
    [Display(Name = "Niveau", Order = 4, GroupName = "Cursus")]
    public int NiveauId { get; set; }

    /// <summary>Niveau (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(NiveauId))]
    [Display(Name = "Niveau")]
    public Niveau? Niveau { get; set; }

    /// <summary>Clé étrangère vers l'année académique.</summary>
    [Display(Name = "Année académique", Order = 2, GroupName = "Cursus")]
    public int AnneeAcadId { get; set; }

    /// <summary>Année académique (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(AnneeAcadId))]
    [Display(Name = "Année académique")]
    public AnneeAcad? AnneeAcad { get; set; }

    /// <summary>Versements sur cette inscription.</summary>
    public ICollection<Paiement> Paiements { get; set; } = new List<Paiement>();

    /// <summary>Notes, une par UE au plus.</summary>
    public ICollection<Note> Notes { get; set; } = new List<Note>();

    /// <summary>Bourses et exonérations accordées.</summary>
    public ICollection<Aide> Aides { get; set; } = new List<Aide>();

    /// <summary>Absences relevées aux séances.</summary>
    public ICollection<Absence> Absences { get; set; } = new List<Absence>();

    /// <summary>Résumé : « AGBEKO Kossi · Informatique L1 · 2026-2027 ».</summary>
    public override string ToString() =>
        $"{Etudiant?.NomComplet} · {Filiere?.Nom} {Niveau?.Code} · {AnneeAcad?.Libelle}";
}
