using System.Collections;
using Microsoft.EntityFrameworkCore;

namespace UniversiteMvc2.Infrastructure;

public interface IPagination
{
    int Page { get; }
    int NombrePages { get; }
    int Total { get; }
}

// Version non typée, pour les vues génériques qui ne connaissent pas T à la compilation
public interface IListePaginee : IPagination
{
    IEnumerable Elements { get; }
    Type TypeElement { get; }
}

// Page de résultats d'une requête, utilisée par toutes les listes longues
public class ListePaginee<T> : IListePaginee
{
    public const int TailleParDefaut = 20;

    public IReadOnlyList<T> Elements { get; }
    public int Page { get; }
    public int Total { get; }
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

    public static async Task<ListePaginee<T>> CreerAsync(IQueryable<T> source, int page, int taille = TailleParDefaut)
    {
        var total = await source.CountAsync();
        var nombrePages = Math.Max(1, (int)Math.Ceiling(total / (double)taille));
        page = Math.Clamp(page, 1, nombrePages);

        var elements = await source.Skip((page - 1) * taille).Take(taille).ToListAsync();
        return new ListePaginee<T>(elements, page, total, taille);
    }
}
