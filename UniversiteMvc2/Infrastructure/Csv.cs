using System.Text;

namespace UniversiteMvc2.Infrastructure;

// Export tableur : point-virgule et BOM UTF-8, pour une ouverture correcte dans Excel en français
public static class Csv
{
    public static byte[] Generer(IEnumerable<string> entetes, IEnumerable<IEnumerable<object?>> lignes)
    {
        var texte = new StringBuilder();
        texte.AppendLine(string.Join(';', entetes.Select(Cellule)));
        foreach (var ligne in lignes)
            texte.AppendLine(string.Join(';', ligne.Select(Cellule)));
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(texte.ToString())];
    }

    private static string Cellule(object? valeur)
    {
        var texte = valeur switch
        {
            null => "",
            DateTime date => Formats.Date(date),
            decimal nombre => nombre.ToString("0.##", Formats.Fr),
            _ => valeur.ToString() ?? ""
        };

        // Une cellule qui commence par =, +, - ou @ serait exécutée comme une formule par le tableur
        if (valeur is string && texte.Length > 0 && "=+-@\t\r".Contains(texte[0]))
            texte = "'" + texte;

        return texte.IndexOfAny([';', '"', '\n', '\r']) >= 0
            ? $"\"{texte.Replace("\"", "\"\"")}\""
            : texte;
    }
}
