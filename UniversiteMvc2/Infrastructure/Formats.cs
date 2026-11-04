using System.Globalization;

namespace UniversiteMvc2.Infrastructure;

/// <summary>Affichage homogène des dates, montants et quantités dans toute l'application</summary>
public static class Formats
{
    /// <summary>Culture française, utilisée pour tout l'affichage.</summary>
    public static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Devise des montants.</summary>
    public const string Devise = "F CFA";

    /// <summary>Montant arrondi à l'unité, avec séparateur de milliers et devise : « 350 000 F CFA ».</summary>
    /// <param name="montant">Montant.</param>
    public static string Montant(decimal montant) => $"{montant.ToString("N0", Fr)} {Devise}";

    /// <summary>Nombre entier avec séparateur de milliers.</summary>
    /// <param name="valeur">Valeur.</param>
    public static string Nombre(decimal valeur) => valeur.ToString("N0", Fr);

    /// <summary>Note sur 20 avec au plus deux décimales, ou « — » sans note.</summary>
    /// <param name="note">Note.</param>
    public static string Note(decimal? note) => note?.ToString("0.##", Fr) ?? "—";

    /// <summary>Date courte : 04/11/2026.</summary>
    /// <param name="date">Date.</param>
    public static string Date(DateTime date) => date.ToString("dd/MM/yyyy", Fr);

    /// <summary>Date en toutes lettres : 4 novembre 2026.</summary>
    /// <param name="date">Date.</param>
    public static string DateLongue(DateTime date) => date.ToString("d MMMM yyyy", Fr);

    /// <summary>Jour de la semaine et date : « Mercredi 4 novembre ».</summary>
    /// <param name="date">Date.</param>
    public static string Jour(DateTime date) => Libelles.Majuscule(date.ToString("dddd d MMMM", Fr));

    /// <summary>Heure : 08h30.</summary>
    /// <param name="heure">Heure du jour.</param>
    public static string Heure(TimeSpan heure) => heure.ToString(@"hh\hmm", Fr);

    /// <summary>Durée en heures : « 2,5 h ».</summary>
    /// <param name="heures">Nombre d'heures.</param>
    public static string Heures(decimal heures) => $"{heures.ToString("0.#", Fr)} h";

    /// <summary>Numéro de reçu d'un paiement : R-000042.</summary>
    /// <param name="paiementId">Identifiant du paiement.</param>
    public static string NumeroRecu(int paiementId) => $"R-{paiementId:000000}";

    /// <summary>Âge en années révolues à la date du jour.</summary>
    /// <param name="naissance">Date de naissance.</param>
    public static int Age(DateTime naissance)
    {
        var aujourdhui = DateTime.Today;
        var age = aujourdhui.Year - naissance.Year;
        return naissance.Date > aujourdhui.AddYears(-age) ? age - 1 : age;
    }

    /// <summary>Pluriel("cours", 3) → "3 cours" ; Pluriel(2, "inscription") → "2 inscriptions"</summary>
    /// <param name="nombre">Quantité.</param>
    /// <param name="singulier">Mot au singulier.</param>
    /// <param name="pluriel">Mot au pluriel ; par défaut, le singulier suivi de s.</param>
    public static string Pluriel(int nombre, string singulier, string? pluriel = null) =>
        $"{Nombre(nombre)} {(nombre > 1 ? pluriel ?? singulier + "s" : singulier)}";
}
