using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

// Unité d'Enseignement
[Entite("unité d'enseignement", "unités d'enseignement", Feminin = true)]
[Groupe("Rattachement", "Filière et niveau dans lesquels l'UE est enseignée.")]
public class UE
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    [Display(Name = "Code", Order = 1, GroupName = "Unité", Prompt = "INF201")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Intitulé", Order = 3, GroupName = "Unité")]
    public string Intitule { get; set; } = string.Empty;

    [Range(1, 30)]
    [Display(Name = "Crédits", Order = 2, GroupName = "Unité")]
    public int Credits { get; set; }

    [Display(Name = "Filière", Order = 4, GroupName = "Rattachement")]
    public int FiliereId { get; set; }

    [ForeignKey(nameof(FiliereId))]
    [Display(Name = "Filière")]
    public Filiere? Filiere { get; set; }

    [Display(Name = "Niveau", Order = 5, GroupName = "Rattachement")]
    public int NiveauId { get; set; }

    [ForeignKey(nameof(NiveauId))]
    [Display(Name = "Niveau")]
    public Niveau? Niveau { get; set; }

    public ICollection<Cours> Cours { get; set; } = new List<Cours>();

    public override string ToString() => $"{Code} — {Intitule}";
}
