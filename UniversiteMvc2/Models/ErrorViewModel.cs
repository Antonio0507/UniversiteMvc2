namespace UniversiteMvc2.Models;

/// <summary>Modèle de la page d'erreur générique.</summary>
public class ErrorViewModel
{
    /// <summary>Référence de la requête, à retrouver dans les logs.</summary>
    public string? RequestId { get; set; }

    /// <summary>Indique s'il y a une référence à afficher.</summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
