using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure;

/// <summary>Une section du sommaire.</summary>
/// <param name="Numero">Numéro affiché dans le menu (« 01 »).</param>
/// <param name="Controleur">Nom du contrôleur.</param>
/// <param name="Titre">Titre de la section.</param>
/// <param name="Groupe">Groupe du menu (Scolarité, Finances…).</param>
/// <param name="Modele">Entité gérée par la section, utilisée par les vues génériques pour créer des liens.</param>
/// <param name="Ecriture">Profils autorisés à modifier la section, en plus de l'administrateur (null = tout utilisateur connecté).</param>
/// <param name="Reserve">Section invisible et inaccessible hors administrateur.</param>
public record Section(string Numero, string Controleur, string Titre, string Groupe, Type? Modele = null, string[]? Ecriture = null, bool Reserve = false);

/// <summary>Sommaire de l'application : ajouter un module = ajouter une ligne ici</summary>
public static class Navigation
{
    private static readonly string[] Scolarite = [Roles.Scolarite];
    private static readonly string[] Comptabilite = [Roles.Comptabilite];

    /// <summary>Les sections, dans l'ordre du menu.</summary>
    public static readonly IReadOnlyList<Section> Sections =
    [
        new("01", "Etudiants", "Étudiants", "Scolarité", typeof(Etudiant), Scolarite),
        new("02", "Inscriptions", "Inscriptions", "Scolarité", typeof(Inscription), Scolarite),
        new("03", "Notes", "Notes", "Scolarité", null, Scolarite),
        new("04", "Deliberations", "Délibérations", "Scolarité", null, Scolarite),
        new("05", "Absences", "Assiduité", "Scolarité", typeof(Absence), Scolarite),
        new("06", "Paiements", "Paiements", "Finances", typeof(Paiement), Comptabilite),
        new("07", "Tarifs", "Tarifs", "Finances", typeof(Tarif), Comptabilite),
        new("08", "Aides", "Bourses et exonérations", "Finances", typeof(Aide), Comptabilite),
        new("09", "Filieres", "Filières", "Enseignement", typeof(Filiere), Scolarite),
        new("10", "Niveaux", "Niveaux", "Enseignement", typeof(Niveau), Scolarite),
        new("11", "UEs", "Unités d'enseignement", "Enseignement", typeof(UE), Scolarite),
        new("12", "Cours", "Cours", "Enseignement", typeof(Cours), Scolarite),
        new("13", "Enseignants", "Enseignants", "Enseignement", typeof(Enseignant), Scolarite),
        new("14", "Seances", "Emploi du temps", "Calendrier", typeof(Seance), Scolarite),
        new("15", "Salles", "Salles", "Calendrier", typeof(Salle), Scolarite),
        new("16", "AnneesAcad", "Années académiques", "Calendrier", typeof(AnneeAcad), Scolarite),
        new("17", "Documents", "Documents PDF", "Édition"),
        new("18", "Comptes", "Comptes", "Administration", Reserve: true),
        new("19", "Journal", "Journal d'audit", "Administration", Reserve: true),
    ];

    /// <summary>Section d'un contrôleur, ou null s'il n'est pas au sommaire.</summary>
    /// <param name="controleur">Nom du contrôleur, sans tenir compte de la casse.</param>
    public static Section? Par(string? controleur) =>
        Sections.FirstOrDefault(s => string.Equals(s.Controleur, controleur, StringComparison.OrdinalIgnoreCase));

    /// <summary>Section de la page affichée.</summary>
    /// <param name="contexte">Contexte de la vue.</param>
    public static Section? Courante(ViewContext contexte) => Par(contexte.RouteData.Values["controller"] as string);

    /// <summary>Sections que l'utilisateur peut consulter.</summary>
    /// <param name="utilisateur">Utilisateur connecté.</param>
    public static IEnumerable<Section> Visibles(ClaimsPrincipal utilisateur) =>
        Sections.Where(s => Droits.PeutLire(utilisateur, s.Controleur));

    /// <summary>Contrôleur qui affiche une entité donnée (null si aucun module ne la gère)</summary>
    /// <param name="modele">Type de l'entité.</param>
    public static string? ControleurDe(Type modele) => Sections.FirstOrDefault(s => s.Modele == modele)?.Controleur;
}
