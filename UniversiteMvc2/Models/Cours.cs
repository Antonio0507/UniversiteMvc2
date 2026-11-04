using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

/// <summary>Cours : partie d'une UE (CM, TD, TP), avec un volume horaire et un enseignant facultatif.</summary>
[Entite("cours", "cours")]
[Groupe("Attribution", "Peut rester vide : le cours apparaîtra dans « À traiter » sur le tableau de bord.")]
public class Cours
{
    /// <summary>Identifiant, attribué par la base.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Intitulé du cours, par exemple « Algorithmique — TD ».</summary>
    [Required]
    [StringLength(100)]
    [Display(Name = "Intitulé", Order = 1, GroupName = "Cours", Prompt = "Algorithmique — TD")]
    public string Intitule { get; set; } = string.Empty;

    /// <summary>Volume horaire annuel, de 1 à 500 heures.</summary>
    [Range(1, 500)]
    [Display(Name = "Volume horaire", Order = 3, GroupName = "Cours", Description = "En heures, sur l'année.")]
    public int VolumeHoraire { get; set; }

    /// <summary>Clé étrangère vers l'UE.</summary>
    [Display(Name = "UE", Order = 2, GroupName = "Cours")]
    public int UEId { get; set; }

    /// <summary>UE du cours (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(UEId))]
    [Display(Name = "UE")]
    public UE? UE { get; set; }

    /// <summary>Clé étrangère vers l'enseignant ; null tant que le cours n'est pas attribué.</summary>
    [Display(Name = "Enseignant", Order = 4, GroupName = "Attribution")]
    public int? EnseignantId { get; set; }

    /// <summary>Enseignant titulaire (propriété de navigation, chargée avec Include).</summary>
    [ForeignKey(nameof(EnseignantId))]
    [Display(Name = "Enseignant")]
    public Enseignant? Enseignant { get; set; }

    /// <summary>Séances programmées à l'emploi du temps.</summary>
    public ICollection<Seance> Seances { get; set; } = new List<Seance>();
}
