using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure;

/// <summary>Règles communes à la création des dossiers et à l'inscription en ligne</summary>
public static class Admissions
{
    /// <summary>Matricule suivant de l'année : ETU2026001, ETU2026002…</summary>
    /// <param name="context">Contexte de base de données.</param>
    public static async Task<string> ProchainMatriculeAsync(this ApplicationDbContext context)
    {
        var prefixe = $"ETU{DateTime.Today.Year}";
        var existants = await context.Etudiants
            .Where(e => e.Matricule.StartsWith(prefixe))
            .Select(e => e.Matricule)
            .ToListAsync();
        var dernier = existants
            .Select(m => int.TryParse(m[prefixe.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefixe}{dernier + 1:000}";
    }

    /// <summary>
    /// Année ouverte aux inscriptions en ligne : l'année en cours, sinon la prochaine ; null si aucune n'est définie.
    /// </summary>
    /// <param name="context">Contexte de base de données.</param>
    public static Task<AnneeAcad?> AnneeOuverteAsync(this ApplicationDbContext context)
    {
        var aujourdhui = DateTime.Today;
        return context.AnneesAcad.AsNoTracking()
            .Where(a => a.DateFin >= aujourdhui)
            .OrderBy(a => a.DateDebut)
            .FirstOrDefaultAsync();
    }
}
