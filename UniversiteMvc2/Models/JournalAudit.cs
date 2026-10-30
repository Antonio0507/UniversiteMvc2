using System.ComponentModel.DataAnnotations;

namespace UniversiteMvc2.Models;

// Trace d'une écriture en base ou d'un événement de sécurité ; alimentée par ApplicationDbContext
public class JournalAudit
{
    [Key]
    public int Id { get; set; }

    public DateTime DateUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(100)]
    public string Auteur { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Entite { get; set; } = string.Empty;

    public int? EntiteId { get; set; }

    [StringLength(2000)]
    public string? Details { get; set; }
}
