namespace UniversiteMvc2.Infrastructure;

/// <summary>
/// Identité de l'établissement imprimée sur les documents PDF (section « Etablissement » d'appsettings.json)
/// </summary>
public class Etablissement
{
    /// <summary>Nom de l'établissement, en tête des pages et des PDF.</summary>
    public string Nom { get; set; } = "Université";
    /// <summary>Service émetteur, sous le nom dans l'en-tête des PDF.</summary>
    public string Service { get; set; } = "Service de la scolarité";
    /// <summary>Adresse postale, en pied de page des PDF.</summary>
    public string Adresse { get; set; } = "";
    /// <summary>Ville, en pied de page et proposée dans « Fait à ».</summary>
    public string Ville { get; set; } = "";
    /// <summary>Téléphone, en pied de page des PDF.</summary>
    public string Telephone { get; set; } = "";
}
