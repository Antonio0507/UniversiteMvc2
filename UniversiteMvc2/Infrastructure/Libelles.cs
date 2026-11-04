using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace UniversiteMvc2.Infrastructure;

// Transforme entités et valeurs en texte lisible pour les vues générées
public static class Libelles
{
    // Propriétés essayées, dans l'ordre, pour nommer une entité qui ne redéfinit pas ToString()
    private static readonly string[] Candidats = ["NomComplet", "Libelle", "Intitule", "Nom", "Code"];

    private static readonly HashSet<Type> Numeriques =
        [typeof(int), typeof(long), typeof(short), typeof(decimal), typeof(double), typeof(float)];

    public static string Texte(object? entite)
    {
        if (entite == null) return "—";

        var type = entite.GetType();
        if (type.GetMethod(nameof(ToString), Type.EmptyTypes)?.DeclaringType != typeof(object))
            return entite.ToString() ?? "—";

        foreach (var nom in Candidats)
            if (type.GetProperty(nom)?.GetValue(entite) is string texte && texte.Length > 0)
                return texte;

        return $"{EntiteAttribute.De(type).Singulier} n° {Id(entite)}";
    }

    public static string Valeur(object? valeur, ModelMetadata metadonnees) => valeur switch
    {
        null => "—",
        string texte => texte.Length == 0 ? "—" : texte,
        DateTime date => Formats.Date(date),
        TimeSpan heure => Formats.Heure(heure),
        decimal montant when metadonnees.DataTypeName == "Currency" => Formats.Montant(montant),
        decimal nombre => Formats.Nombre(nombre),
        bool vrai => vrai ? "Oui" : "Non",
        _ when valeur.GetType().IsClass => Texte(valeur),
        _ => valeur.ToString() ?? "—"
    };

    public static int? Id(object entite) => entite.GetType().GetProperty("Id")?.GetValue(entite) as int?;

    public static bool EstNumerique(Type type) => Numeriques.Contains(Nullable.GetUnderlyingType(type) ?? type);

    // Codes et matricules en chasse fixe
    public static bool EstCode(string nomPropriete) => nomPropriete is "Code" or "Matricule";

    public static string Majuscule(string texte) =>
        string.IsNullOrEmpty(texte) ? texte : char.ToUpper(texte[0], Formats.Fr) + texte[1..];

    public static string Minuscule(string texte) =>
        string.IsNullOrEmpty(texte) ? texte : char.ToLower(texte[0], Formats.Fr) + texte[1..];
}
