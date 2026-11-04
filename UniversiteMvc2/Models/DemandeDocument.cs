using System.ComponentModel.DataAnnotations;

namespace UniversiteMvc2.Models;

public enum TypeDocument
{
    [Display(Name = "Attestation d'inscription", Description = "Certifie qu'un étudiant est inscrit pour l'année. Inscriptions validées uniquement.")]
    AttestationInscription,

    [Display(Name = "Relevé des paiements", Description = "Tous les versements d'une inscription, avec le total arrêté.")]
    RelevePaiements,

    [Display(Name = "Liste des inscrits", Description = "Par année, avec filtres facultatifs : filière, niveau, statut.")]
    ListeInscrits,

    [Display(Name = "Reçu de paiement", Description = "Le reçu d'un versement, prêt à remettre à l'étudiant.")]
    RecuPaiement,

    [Display(Name = "Relevé de notes", Description = "Notes retenues, moyenne pondérée et crédits validés. Inscriptions validées uniquement.")]
    ReleveNotes,

    [Display(Name = "Procès-verbal de délibération", Description = "Résultats d'une promotion : rang, moyenne, crédits et décision. Année, filière et niveau requis.")]
    ProcesVerbal
}

// Formulaire de génération d'un document PDF ; les champs utiles dépendent du type choisi
public class DemandeDocument
{
    [Display(Name = "Document")]
    public TypeDocument Type { get; set; }

    [Display(Name = "Inscription")]
    public int? InscriptionId { get; set; }

    [Display(Name = "Paiement")]
    public int? PaiementId { get; set; }

    [Display(Name = "Année académique")]
    public int? AnneeAcadId { get; set; }

    [Display(Name = "Filière")]
    public int? FiliereId { get; set; }

    [Display(Name = "Niveau")]
    public int? NiveauId { get; set; }

    [Display(Name = "Statut")]
    public string? Statut { get; set; }

    [Required]
    [StringLength(80)]
    [Display(Name = "Signataire", Description = "Titre de la personne qui signe le document.")]
    public string Signataire { get; set; } = "Le chef du service de la scolarité";

    [Required]
    [StringLength(60)]
    [Display(Name = "Fait à")]
    public string Lieu { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Date du document")]
    public DateTime DateDocument { get; set; } = DateTime.Today;
}
