using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure;

public sealed record LigneImpaye(Inscription Inscription, decimal Du, decimal Verse)
{
    public decimal Reste => Math.Max(0, Du - Verse);
}

// Calcule ce que doit une inscription à partir des tarifs et des versements
public static class Soldes
{
    public static async Task<decimal?> TarifDeAsync(this ApplicationDbContext context, Inscription inscription) =>
        await context.Tarifs
            .Where(t => t.AnneeAcadId == inscription.AnneeAcadId && t.FiliereId == inscription.FiliereId && t.NiveauId == inscription.NiveauId)
            .Select(t => (decimal?)t.Montant)
            .FirstOrDefaultAsync();

    // Inscriptions validées de l'année dont le tarif n'est pas entièrement réglé, les plus gros restes d'abord
    public static async Task<List<LigneImpaye>> ImpayesAsync(this ApplicationDbContext context, int anneeId)
    {
        var tarifs = await context.Tarifs.AsNoTracking().Where(t => t.AnneeAcadId == anneeId).ToListAsync();
        if (tarifs.Count == 0) return [];

        var montants = tarifs.ToDictionary(t => (t.FiliereId, t.NiveauId), t => t.Montant);
        var inscriptions = await context.Inscriptions.AsNoTracking()
            .Include(i => i.Etudiant).Include(i => i.Filiere).Include(i => i.Niveau).Include(i => i.Paiements)
            .Where(i => i.AnneeAcadId == anneeId && i.Statut == Inscription.Validee)
            .AsSplitQuery()
            .ToListAsync();

        return inscriptions
            .Where(i => montants.ContainsKey((i.FiliereId, i.NiveauId)))
            .Select(i => new LigneImpaye(i, montants[(i.FiliereId, i.NiveauId)], i.Paiements.Sum(p => p.Montant)))
            .Where(l => l.Reste > 0)
            .OrderByDescending(l => l.Reste).ThenBy(l => l.Inscription.Etudiant?.Nom)
            .ToList();
    }
}
