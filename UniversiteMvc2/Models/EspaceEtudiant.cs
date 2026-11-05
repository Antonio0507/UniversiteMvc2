namespace UniversiteMvc2.Models;

/// <summary>Contenu de la page « Mon espace » d'un étudiant connecté.</summary>
public class EspaceEtudiant
{
    /// <summary>Dossier de l'étudiant.</summary>
    public required Etudiant Etudiant { get; init; }

    /// <summary>Ses inscriptions, la plus récente d'abord.</summary>
    public required List<Inscription> Inscriptions { get; init; }

    /// <summary>Inscription affichée (paiements, notes, aides et absences chargés), ou null s'il n'en a aucune.</summary>
    public Inscription? Inscription { get; init; }

    /// <summary>Tarif de l'inscription affichée, ou null sans tarif.</summary>
    public decimal? Tarif { get; init; }

    /// <summary>Frais dus (aides déduites) et versé.</summary>
    public Solde? Solde { get; init; }

    /// <summary>UE du cursus de l'inscription affichée.</summary>
    public List<UE> UEs { get; init; } = [];

    /// <summary>Moyenne, crédits et décision de l'inscription affichée.</summary>
    public Bilan? Bilan { get; init; }

    /// <summary>Lundi de la semaine d'emploi du temps affichée.</summary>
    public DateTime Lundi { get; init; }

    /// <summary>Séances de la semaine pour la filière et le niveau de l'inscription affichée.</summary>
    public List<Seance> Seances { get; init; } = [];

    /// <summary>Année ouverte à la réinscription, si l'étudiant n'y est pas encore inscrit.</summary>
    public AnneeAcad? Reinscription { get; init; }
}
