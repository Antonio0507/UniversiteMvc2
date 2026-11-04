using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure;

/// <summary>Une inscription dont le reste à payer est positif.</summary>
/// <param name="Inscription">Inscription validée.</param>
/// <param name="Du">Frais dus, aides déduites.</param>
/// <param name="Verse">Total des versements.</param>
public sealed record LigneImpaye(Inscription Inscription, decimal Du, decimal Verse)
{
    /// <summary>Reste à payer, jamais négatif.</summary>
    public decimal Reste => Math.Max(0, Du - Verse);
}

/// <summary>
/// Calcule ce que doit une inscription à partir des tarifs, des bourses et exonérations, et des versements
/// </summary>
public static class Soldes
{
    /// <summary>Tarif annuel qui s'applique à une inscription (année, filière, niveau).</summary>
    /// <param name="context">Contexte de base de données.</param>
    /// <param name="inscription">Inscription.</param>
    /// <returns>Le montant, ou null s'il n'y a pas de tarif.</returns>
    public static async Task<decimal?> TarifDeAsync(this ApplicationDbContext context, Inscription inscription) =>
        await context.Tarifs
            .Where(t => t.AnneeAcadId == inscription.AnneeAcadId && t.FiliereId == inscription.FiliereId && t.NiveauId == inscription.NiveauId)
            .Select(t => (decimal?)t.Montant)
            .FirstOrDefaultAsync();

    /// <summary>Total des bourses et exonérations accordées sur une inscription.</summary>
    /// <param name="context">Contexte de base de données.</param>
    /// <param name="inscriptionId">Identifiant de l'inscription.</param>
    public static async Task<decimal> AidesDeAsync(this ApplicationDbContext context, int inscriptionId) =>
        await context.Aides.Where(a => a.InscriptionId == inscriptionId).SumAsync(a => (decimal?)a.Montant) ?? 0;

    /// <summary>Frais de scolarité, aides déduites ; null si aucun tarif ne s'applique</summary>
    /// <param name="context">Contexte de base de données.</param>
    /// <param name="inscription">Inscription.</param>
    public static async Task<decimal?> DuAsync(this ApplicationDbContext context, Inscription inscription)
    {
        var tarif = await context.TarifDeAsync(inscription);
        return tarif == null ? null : Solde.Net(tarif.Value, await context.AidesDeAsync(inscription.Id));
    }

    /// <summary>
    /// Inscriptions validées de l'année dont le tarif n'est pas entièrement réglé, les plus gros restes d'abord
    /// </summary>
    /// <param name="context">Contexte de base de données.</param>
    /// <param name="anneeId">Année académique.</param>
    public static async Task<List<LigneImpaye>> ImpayesAsync(this ApplicationDbContext context, int anneeId)
    {
        var tarifs = await context.Tarifs.AsNoTracking().Where(t => t.AnneeAcadId == anneeId).ToListAsync();
        if (tarifs.Count == 0) return [];

        var montants = tarifs.ToDictionary(t => (t.FiliereId, t.NiveauId), t => t.Montant);
        var inscriptions = await context.Inscriptions.AsNoTracking()
            .Include(i => i.Etudiant).Include(i => i.Filiere).Include(i => i.Niveau).Include(i => i.Paiements).Include(i => i.Aides)
            .Where(i => i.AnneeAcadId == anneeId && i.Statut == Inscription.Validee)
            .AsSplitQuery()
            .ToListAsync();

        return inscriptions
            .Where(i => montants.ContainsKey((i.FiliereId, i.NiveauId)))
            .Select(i => new LigneImpaye(i, Solde.Net(montants[(i.FiliereId, i.NiveauId)], i.Aides.Sum(a => a.Montant)), i.Paiements.Sum(p => p.Montant)))
            .Where(l => l.Reste > 0)
            .OrderByDescending(l => l.Reste).ThenBy(l => l.Inscription.Etudiant?.Nom)
            .ToList();
    }
}
