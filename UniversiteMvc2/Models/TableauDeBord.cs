namespace UniversiteMvc2.Models;

/// <summary>Un effectif dans une répartition du tableau de bord.</summary>
/// <param name="Code">Code court (filière ou statut).</param>
/// <param name="Libelle">Libellé complet.</param>
/// <param name="Nombre">Nombre d'inscriptions.</param>
public record Repartition(string Code, string Libelle, int Nombre);

/// <summary>Données de la page d'accueil, pour une année académique.</summary>
public class TableauDeBord
{
    /// <summary>Année affichée, ou null s'il n'existe aucune année.</summary>
    public AnneeAcad? Annee { get; init; }
    /// <summary>Toutes les années, pour la liste de choix.</summary>
    public IReadOnlyList<AnneeAcad> Annees { get; init; } = [];

    /// <summary>Nombre d'étudiants au registre, toutes années confondues.</summary>
    public int Etudiants { get; init; }
    /// <summary>Nombre d'enseignants.</summary>
    public int Enseignants { get; init; }
    /// <summary>Nombre d'inscriptions de l'année.</summary>
    public int Inscriptions { get; init; }
    /// <summary>Total encaissé sur l'année.</summary>
    public decimal Encaisse { get; init; }
    /// <summary>Total restant à encaisser sur les inscriptions validées de l'année.</summary>
    public decimal ResteAEncaisser { get; init; }
    /// <summary>Nombre d'inscriptions en impayé.</summary>
    public int Impayes { get; init; }

    /// <summary>Inscriptions de l'année par filière.</summary>
    public IReadOnlyList<Repartition> ParFiliere { get; init; } = [];
    /// <summary>Inscriptions de l'année par statut.</summary>
    public IReadOnlyList<Repartition> ParStatut { get; init; } = [];

    /// <summary>Inscriptions en attente de validation.</summary>
    public int InscriptionsEnAttente { get; init; }
    /// <summary>Inscriptions validées sans aucun paiement.</summary>
    public int InscriptionsSansPaiement { get; init; }
    /// <summary>Cours sans enseignant.</summary>
    public int CoursNonAttribues { get; init; }

    /// <summary>Les 6 dernières inscriptions.</summary>
    public IReadOnlyList<Inscription> DernieresInscriptions { get; init; } = [];
    /// <summary>Les 6 derniers paiements.</summary>
    public IReadOnlyList<Paiement> DerniersPaiements { get; init; } = [];

    /// <summary>Part de l'année académique écoulée, entre 0 et 1</summary>
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
