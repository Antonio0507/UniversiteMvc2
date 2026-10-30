using System.Globalization;

namespace UniversiteMvc2.Infrastructure;

// Affichage homogène des dates, montants et quantités dans toute l'application
public static class Formats
{
    public static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public const string Devise = "F CFA";

    public static string Montant(decimal montant) => $"{montant.ToString("N0", Fr)} {Devise}";

    public static string Nombre(decimal valeur) => valeur.ToString("N0", Fr);

    public static string Note(decimal? note) => note?.ToString("0.##", Fr) ?? "—";

    public static string Date(DateTime date) => date.ToString("dd/MM/yyyy", Fr);

    public static string DateLongue(DateTime date) => date.ToString("d MMMM yyyy", Fr);

    public static string NumeroRecu(int paiementId) => $"R-{paiementId:000000}";

    public static int Age(DateTime naissance)
    {
        var aujourdhui = DateTime.Today;
        var age = aujourdhui.Year - naissance.Year;
        return naissance.Date > aujourdhui.AddYears(-age) ? age - 1 : age;
    }

    // Pluriel("cours", 3) → "3 cours" ; Pluriel(2, "inscription") → "2 inscriptions"
    public static string Pluriel(int nombre, string singulier, string? pluriel = null) =>
        $"{Nombre(nombre)} {(nombre > 1 ? pluriel ?? singulier + "s" : singulier)}";
}
