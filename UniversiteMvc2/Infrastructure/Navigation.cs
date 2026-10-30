using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure;

// Modele : entité gérée par le module, utilisée par les vues génériques pour créer des liens
// Ecriture : profils autorisés à modifier le module, en plus de l'administrateur (null = tout utilisateur connecté)
// Reserve : module invisible et inaccessible hors administrateur
public record Section(string Numero, string Controleur, string Titre, string Groupe, Type? Modele = null, string[]? Ecriture = null, bool Reserve = false);

// Sommaire de l'application : ajouter un module = ajouter une ligne ici
public static class Navigation
{
    private static readonly string[] Scolarite = [Roles.Scolarite];
    private static readonly string[] Comptabilite = [Roles.Comptabilite];

    public static readonly IReadOnlyList<Section> Sections =
    [
        new("01", "Etudiants", "Étudiants", "Scolarité", typeof(Etudiant), Scolarite),
        new("02", "Inscriptions", "Inscriptions", "Scolarité", typeof(Inscription), Scolarite),
        new("03", "Notes", "Notes", "Scolarité", null, Scolarite),
        new("04", "Paiements", "Paiements", "Finances", typeof(Paiement), Comptabilite),
        new("05", "Tarifs", "Tarifs", "Finances", typeof(Tarif), Comptabilite),
        new("06", "Filieres", "Filières", "Enseignement", typeof(Filiere), Scolarite),
        new("07", "Niveaux", "Niveaux", "Enseignement", typeof(Niveau), Scolarite),
        new("08", "UEs", "Unités d'enseignement", "Enseignement", typeof(UE), Scolarite),
        new("09", "Cours", "Cours", "Enseignement", typeof(Cours), Scolarite),
        new("10", "Enseignants", "Enseignants", "Enseignement", typeof(Enseignant), Scolarite),
        new("11", "AnneesAcad", "Années académiques", "Calendrier", typeof(AnneeAcad), Scolarite),
        new("12", "Documents", "Documents PDF", "Édition"),
        new("13", "Comptes", "Comptes", "Administration", Reserve: true),
        new("14", "Journal", "Journal d'audit", "Administration", Reserve: true),
    ];

    public static Section? Par(string? controleur) =>
        Sections.FirstOrDefault(s => string.Equals(s.Controleur, controleur, StringComparison.OrdinalIgnoreCase));

    public static Section? Courante(ViewContext contexte) => Par(contexte.RouteData.Values["controller"] as string);

    public static IEnumerable<Section> Visibles(ClaimsPrincipal utilisateur) =>
        Sections.Where(s => Droits.PeutLire(utilisateur, s.Controleur));

    // Contrôleur qui affiche une entité donnée (null si aucun module ne la gère)
    public static string? ControleurDe(Type modele) => Sections.FirstOrDefault(s => s.Modele == modele)?.Controleur;
}
