using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure;

// Rang : place dans la promotion (filière + niveau), à la moyenne ; null sans moyenne
public sealed record LigneDeliberation(Inscription Inscription, Bilan Bilan, int? Rang);

// Résultats de fin d'année : bilan et décision de chaque inscription validée (page Délibérations et procès-verbal PDF)
public static class Deliberations
{
    public static async Task<List<LigneDeliberation>> CalculerAsync(this ApplicationDbContext context, int anneeId, int? filiereId, int? niveauId)
    {
        var requete = context.Inscriptions.AsNoTracking()
            .Include(i => i.Etudiant).Include(i => i.Filiere).Include(i => i.Niveau).Include(i => i.Notes)
            .Where(i => i.AnneeAcadId == anneeId && i.Statut == Inscription.Validee);
        if (filiereId != null) requete = requete.Where(i => i.FiliereId == filiereId);
        if (niveauId != null) requete = requete.Where(i => i.NiveauId == niveauId);
        var inscriptions = await requete.AsSplitQuery().ToListAsync();

        var cursus = inscriptions.Select(i => (i.FiliereId, i.NiveauId)).Distinct().ToList();
        var filieres = cursus.Select(c => c.FiliereId).Distinct().ToList();
        var ues = (await context.UEs.AsNoTracking().Where(u => filieres.Contains(u.FiliereId)).ToListAsync())
            .ToLookup(u => (u.FiliereId, u.NiveauId));

        var lignes = inscriptions.Select(i => (Inscription: i, Bilan: Bilan.Calculer(ues[(i.FiliereId, i.NiveauId)]
            .Select(u => (u.Credits, i.Notes.FirstOrDefault(n => n.UEId == u.Id)?.Retenue))
            .ToList()))).ToList();

        return lignes
            .GroupBy(l => (l.Inscription.FiliereId, l.Inscription.NiveauId))
            .SelectMany(promotion => Classer(promotion.Select(l => (l.Inscription, l.Bilan))))
            .OrderBy(l => l.Inscription.Filiere?.Code).ThenBy(l => l.Inscription.NiveauId)
            .ThenBy(l => l.Rang ?? int.MaxValue).ThenBy(l => l.Inscription.Etudiant?.Nom).ThenBy(l => l.Inscription.Etudiant?.Prenom)
            .ToList();
    }

    // Classement à la moyenne ; les ex aequo partagent le même rang (1, 2, 2, 4…)
    public static IEnumerable<LigneDeliberation> Classer(IEnumerable<(Inscription Inscription, Bilan Bilan)> promotion)
    {
        var lignes = promotion.ToList();
        foreach (var (inscription, bilan) in lignes)
        {
            int? rang = bilan.Moyenne == null ? null : 1 + lignes.Count(l => l.Bilan.Moyenne > bilan.Moyenne);
            yield return new LigneDeliberation(inscription, bilan, rang);
        }
    }
}
