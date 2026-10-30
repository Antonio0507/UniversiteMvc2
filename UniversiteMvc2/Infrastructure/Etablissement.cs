namespace UniversiteMvc2.Infrastructure;

// Identité de l'établissement imprimée sur les documents PDF (section « Etablissement » d'appsettings.json)
public class Etablissement
{
    public string Nom { get; set; } = "Université";
    public string Service { get; set; } = "Service de la scolarité";
    public string Adresse { get; set; } = "";
    public string Ville { get; set; } = "";
    public string Telephone { get; set; } = "";
}
