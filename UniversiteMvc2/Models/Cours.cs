using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniversiteMvc2.Infrastructure;

namespace UniversiteMvc2.Models;

[Entite("cours", "cours")]
[Groupe("Attribution", "Peut rester vide : le cours apparaîtra dans « À traiter » sur le tableau de bord.")]
public class Cours
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Intitulé", Order = 1, GroupName = "Cours", Prompt = "Algorithmique — TD")]
    public string Intitule { get; set; } = string.Empty;

    [Range(1, 500)]
    [Display(Name = "Volume horaire", Order = 3, GroupName = "Cours", Description = "En heures, sur l'année.")]
    public int VolumeHoraire { get; set; }

    [Display(Name = "UE", Order = 2, GroupName = "Cours")]
    public int UEId { get; set; }

    [ForeignKey(nameof(UEId))]
    [Display(Name = "UE")]
    public UE? UE { get; set; }

    [Display(Name = "Enseignant", Order = 4, GroupName = "Attribution")]
    public int? EnseignantId { get; set; }

    [ForeignKey(nameof(EnseignantId))]
    [Display(Name = "Enseignant")]
    public Enseignant? Enseignant { get; set; }
}
