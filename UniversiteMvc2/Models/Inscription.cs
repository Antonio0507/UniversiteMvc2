using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

[Entite("inscription", "inscriptions", Feminin = true)]
[Groupe("Cursus", "Un étudiant ne peut être inscrit qu'une fois par année académique.")]
public class Inscription
{
    public const string EnAttente = "En attente";
    public const string Validee = "Validée";
    public const string Annulee = "Annulée";
    public static readonly string[] Statuts = [EnAttente, Validee, Annulee];

    [Key]
    public int Id { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date d'inscription", Order = 5, GroupName = "Dossier")]
    public DateTime DateInscription { get; set; } = DateTime.Today;

    [Required]
    [StringLength(20)]
    [AllowedValues(EnAttente, Validee, Annulee, ErrorMessage = "Statut inconnu.")]
    [Display(Name = "Statut", Order = 6, GroupName = "Dossier")]
    public string Statut { get; set; } = EnAttente;

    [Display(Name = "Étudiant", Order = 1, GroupName = "Étudiant", Description = "Absent de la liste ? Créez d'abord son dossier dans Étudiants.")]
    public int EtudiantId { get; set; }

    [ForeignKey(nameof(EtudiantId))]
    [Display(Name = "Étudiant")]
    public Etudiant? Etudiant { get; set; }

    [Display(Name = "Filière", Order = 3, GroupName = "Cursus")]
    public int FiliereId { get; set; }

    [ForeignKey(nameof(FiliereId))]
    [Display(Name = "Filière")]
    public Filiere? Filiere { get; set; }

    [Display(Name = "Niveau", Order = 4, GroupName = "Cursus")]
    public int NiveauId { get; set; }

    [ForeignKey(nameof(NiveauId))]
    [Display(Name = "Niveau")]
    public Niveau? Niveau { get; set; }

    [Display(Name = "Année académique", Order = 2, GroupName = "Cursus")]
    public int AnneeAcadId { get; set; }

    [ForeignKey(nameof(AnneeAcadId))]
    [Display(Name = "Année académique")]
    public AnneeAcad? AnneeAcad { get; set; }

    public ICollection<Paiement> Paiements { get; set; } = new List<Paiement>();

    public ICollection<Note> Notes { get; set; } = new List<Note>();

    public override string ToString() =>
        $"{Etudiant?.NomComplet} · {Filiere?.Nom} {Niveau?.Code} · {AnneeAcad?.Libelle}";
}
