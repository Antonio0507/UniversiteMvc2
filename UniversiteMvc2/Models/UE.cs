using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>
/// Unité d'enseignement : matière notée, porteuse de crédits, enseignée dans une filière et un niveau.
/// </summary>
[Entite("unité d'enseignement", "unités d'enseignement", Feminin = true)]
[Groupe("Rattachement", "Filière et niveau dans lesquels l'UE est enseignée.")]
public class UE
{
    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Code court et unique, en majuscules (INF201).</summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "Code", Order = 1, GroupName = "Unité", Prompt = "INF201")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Intitulé de l'UE.</summary>
    [Required]
    [StringLength(100)]
    [Display(Name = "Intitulé", Order = 3, GroupName = "Unité")]
    public string Intitule { get; set; } = string.Empty;

    /// <summary>Crédits LMD, de 1 à 30 ; acquis quand l'UE est validée.</summary>
    [Range(1, 30)]
    [Display(Name = "Crédits", Order = 2, GroupName = "Unité")]
    public int Credits { get; set; }

    /// <summary>Clé étrangère vers la filière.</summary>
    [Display(Name = "Filière", Order = 4, GroupName = "Rattachement")]
    public int FiliereId { get; set; }

    /// <summary>Filière (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(FiliereId))]
    [Display(Name = "Filière")]
    public Filiere? Filiere { get; set; }

    /// <summary>Clé étrangère vers le niveau.</summary>
    [Display(Name = "Niveau", Order = 5, GroupName = "Rattachement")]
    public int NiveauId { get; set; }

    /// <summary>Niveau (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(NiveauId))]
    [Display(Name = "Niveau")]
    public Niveau? Niveau { get; set; }

    /// <summary>Cours de l'UE ; supprimés avec elle.</summary>
    public ICollection<Cours> Cours { get; set; } = new List<Cours>();

    /// <summary>Code et intitulé : « INF201 — Algorithmique ».</summary>
    public override string ToString() => $"{Code} — {Intitule}";
}
