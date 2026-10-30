namespace UniversiteMvc2.Models;

// Page de confirmation de suppression partagée par tous les modules.
// Blocages : ce qui empêche la suppression ; Effets : ce qu'elle entraîne.
public record Suppression(string Titre, string Libelle, int Id, IReadOnlyList<string> Blocages, IReadOnlyList<string> Effets)
{
    public bool Possible => Blocages.Count == 0;
}
