using System.Collections;
using Microsoft.EntityFrameworkCore;

namespace UniversiteMvc2.Infrastructure;

/// <summary>Informations de pagination, utilisées par la vue partielle _Pagination.</summary>
public interface IPagination
{
    /// <summary>Numéro de la page affichée, à partir de 1.</summary>
    int Page { get; }
    /// <summary>Nombre total de pages.</summary>
    int NombrePages { get; }
    /// <summary>Nombre total d'éléments, toutes pages confondues.</summary>
    int Total { get; }
}

/// <summary>Version non typée, pour les vues génériques qui ne connaissent pas T à la compilation</summary>
public interface IListePaginee : IPagination
{
    /// <summary>Éléments de la page.</summary>
    IEnumerable Elements { get; }
    /// <summary>Type des éléments.</summary>
    Type TypeElement { get; }
}

/// <summary>Page de résultats d'une requête, utilisée par toutes les listes longues</summary>
public class ListePaginee<T> : IListePaginee
{
    /// <summary>Nombre d'éléments par page, sauf indication contraire.</summary>
    public const int TailleParDefaut = 20;

    /// <summary>Éléments de la page affichée.</summary>
    public IReadOnlyList<T> Elements { get; }
    /// <summary>Numéro de la page affichée, à partir de 1.</summary>
    public int Page { get; }
    /// <summary>Nombre total d'éléments.</summary>
    public int Total { get; }
    /// <summary>Nombre total de pages (au moins 1).</summary>
    public int NombrePages { get; }

    IEnumerable IListePaginee.Elements => Elements;
    Type IListePaginee.TypeElement => typeof(T);

    private ListePaginee(IReadOnlyList<T> elements, int page, int total, int taille)
    {
        Elements = elements;
        Page = page;
        Total = total;
        NombrePages = Math.Max(1, (int)Math.Ceiling(total / (double)taille));
    }

    /// <summary>
    /// Exécute la requête pour une page ; un numéro hors bornes est ramené à la première ou à la dernière page.
    /// </summary>
    /// <param name="source">Requête déjà filtrée et triée.</param>
    /// <param name="page">Numéro de page demandé.</param>
    /// <param name="taille">Nombre d'éléments par page.</param>
    public static async Task<ListePaginee<T>> CreerAsync(IQueryable<T> source, int page, int taille = TailleParDefaut)
    {
        var total = await source.CountAsync();
        var nombrePages = Math.Max(1, (int)Math.Ceiling(total / (double)taille));
        page = Math.Clamp(page, 1, nombrePages);

        var elements = await source.Skip((page - 1) * taille).Take(taille).ToListAsync();
        return new ListePaginee<T>(elements, page, total, taille);
    }
}
