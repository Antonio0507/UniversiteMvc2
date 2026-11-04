using System.ComponentModel.DataAnnotations;

namespace UniversiteMvc2.Models;

/// <summary>Documents PDF que l'application sait produire.</summary>
public enum TypeDocument
{
    /// <summary>Attestation d'inscription (inscription validée).</summary>
    [Display(Name = "Attestation d'inscription", Description = "Certifie qu'un étudiant est inscrit pour l'année. Inscriptions validées uniquement.")]
    AttestationInscription,

    /// <summary>Relevé des paiements d'une inscription.</summary>
    [Display(Name = "Relevé des paiements", Description = "Tous les versements d'une inscription, avec le total arrêté.")]
    RelevePaiements,

    /// <summary>Liste des inscrits d'une année, avec filtres.</summary>
    [Display(Name = "Liste des inscrits", Description = "Par année, avec filtres facultatifs : filière, niveau, statut.")]
    ListeInscrits,

    /// <summary>Reçu d'un paiement.</summary>
    [Display(Name = "Reçu de paiement", Description = "Le reçu d'un versement, prêt à remettre à l'étudiant.")]
    RecuPaiement,

    /// <summary>Relevé de notes (inscription validée).</summary>
    [Display(Name = "Relevé de notes", Description = "Notes retenues, moyenne pondérée et crédits validés. Inscriptions validées uniquement.")]
    ReleveNotes,

    /// <summary>Procès-verbal de délibération d'une promotion.</summary>
    [Display(Name = "Procès-verbal de délibération", Description = "Résultats d'une promotion : rang, moyenne, crédits et décision. Année, filière et niveau requis.")]
    ProcesVerbal
}

/// <summary>Formulaire de génération d'un document PDF ; les champs utiles dépendent du type choisi</summary>
public class DemandeDocument
{
    /// <summary>Document demandé.</summary>
    [Display(Name = "Document")]
    public TypeDocument Type { get; set; }

    /// <summary>Inscription (attestation, relevés).</summary>
    [Display(Name = "Inscription")]
    public int? InscriptionId { get; set; }

    /// <summary>Paiement (reçu).</summary>
    [Display(Name = "Paiement")]
    public int? PaiementId { get; set; }

    /// <summary>Année (liste des inscrits, procès-verbal).</summary>
    [Display(Name = "Année académique")]
    public int? AnneeAcadId { get; set; }

    /// <summary>Filière (filtre de la liste ; obligatoire pour le procès-verbal).</summary>
    [Display(Name = "Filière")]
    public int? FiliereId { get; set; }

    /// <summary>Niveau (filtre de la liste ; obligatoire pour le procès-verbal).</summary>
    [Display(Name = "Niveau")]
    public int? NiveauId { get; set; }

    /// <summary>Statut d'inscription (filtre de la liste).</summary>
    [Display(Name = "Statut")]
    public string? Statut { get; set; }

    /// <summary>Titre de la personne qui signe le document.</summary>
    [Required]
    [StringLength(80)]
    [Display(Name = "Signataire", Description = "Titre de la personne qui signe le document.")]
    public string Signataire { get; set; } = "Le chef du service de la scolarité";

    /// <summary>Ville imprimée dans « Fait à ».</summary>
    [Required]
    [StringLength(60)]
    [Display(Name = "Fait à")]
    public string Lieu { get; set; } = string.Empty;

    /// <summary>Date imprimée sur le document.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Date du document")]
    public DateTime DateDocument { get; set; } = DateTime.Today;
}
