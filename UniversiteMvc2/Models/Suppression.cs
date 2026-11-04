namespace UniversiteMvc2.Models;

/// <summary>Page de confirmation de suppression partagée par tous les modules.</summary>
/// <param name="Titre">Titre de la page.</param>
/// <param name="Libelle">Nom de l'élément à supprimer.</param>
/// <param name="Id">Identifiant de l'élément.</param>
/// <param name="Blocages">Ce qui empêche la suppression.</param>
/// <param name="Effets">Ce que la suppression entraîne.</param>
public record Suppression(string Titre, string Libelle, int Id, IReadOnlyList<string> Blocages, IReadOnlyList<string> Effets)
{
    /// <summary>La suppression est possible s'il n'y a aucun blocage.</summary>
    public bool Possible => Blocages.Count == 0;
}
