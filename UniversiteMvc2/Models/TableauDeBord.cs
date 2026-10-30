namespace UniversiteMvc2.Models;

public record Repartition(string Code, string Libelle, int Nombre);

public class TableauDeBord
{
    public AnneeAcad? Annee { get; init; }
    public IReadOnlyList<AnneeAcad> Annees { get; init; } = [];

    public int Etudiants { get; init; }
    public int Enseignants { get; init; }
    public int Inscriptions { get; init; }
    public decimal Encaisse { get; init; }
    public decimal ResteAEncaisser { get; init; }
    public int Impayes { get; init; }

    public IReadOnlyList<Repartition> ParFiliere { get; init; } = [];
    public IReadOnlyList<Repartition> ParStatut { get; init; } = [];

    public int InscriptionsEnAttente { get; init; }
    public int InscriptionsSansPaiement { get; init; }
    public int CoursNonAttribues { get; init; }

    public IReadOnlyList<Inscription> DernieresInscriptions { get; init; } = [];
    public IReadOnlyList<Paiement> DerniersPaiements { get; init; } = [];

    // Part de l'année académique écoulée, entre 0 et 1
    public double Avancement
    {
        get
        {
            if (Annee == null) return 0;
            var duree = (Annee.DateFin - Annee.DateDebut).TotalDays;
            if (duree <= 0) return 0;
            return Math.Clamp((DateTime.Today - Annee.DateDebut).TotalDays / duree, 0, 1);
        }
    }
}
