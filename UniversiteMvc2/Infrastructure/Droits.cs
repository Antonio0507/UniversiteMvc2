using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Razor.TagHelpers;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure;

/// <summary>
/// Règles d'accès, déduites du sommaire (Navigation) : une seule source pour le contrôle serveur et l'affichage des
/// liens
/// </summary>
public static class Droits
{
    /// <summary>Claim posé tant que le compte doit changer son mot de passe provisoire.</summary>
    public const string ClaimDoitChanger = "doit_changer_mot_de_passe";
    /// <summary>
    /// Claim qui porte le jeton de session du compte ; s'il ne correspond plus à la base, la session est rejetée.
    /// </summary>
    public const string ClaimJeton = "jeton";

    // Actions GET qui ouvrent une écriture, ou sortent des données en masse
    private static readonly HashSet<string> ActionsEcriture = new(StringComparer.OrdinalIgnoreCase)
    {
        "Create", "Edit", "Delete", "Exporter", "ExporterImpayes"
    };

    /// <summary>
    /// Indique si l'utilisateur peut consulter une section : toutes, sauf les sections réservées hors administrateur.
    /// </summary>
    /// <param name="utilisateur">Utilisateur connecté.</param>
    /// <param name="controleur">Nom du contrôleur.</param>
    public static bool PeutLire(ClaimsPrincipal utilisateur, string? controleur)
    {
        var section = Navigation.Par(controleur);
        return section == null || !section.Reserve || utilisateur.IsInRole(Roles.Administrateur);
    }

    /// <summary>
    /// Indique si l'utilisateur peut modifier une section : l'administrateur partout, les autres profils dans leurs
    /// sections.
    /// </summary>
    /// <param name="utilisateur">Utilisateur connecté.</param>
    /// <param name="controleur">Nom du contrôleur.</param>
    public static bool PeutEcrire(ClaimsPrincipal utilisateur, string? controleur)
    {
        var section = Navigation.Par(controleur);
        if (section == null) return true;
        if (utilisateur.IsInRole(Roles.Administrateur)) return true;
        if (section.Reserve) return false;
        return section.Ecriture == null || section.Ecriture.Any(utilisateur.IsInRole);
    }

    /// <summary>
    /// Indique si une action est autorisée. Toute requête non GET, et les actions Create, Edit, Delete, Exporter et
    /// ExporterImpayes, exigent le droit d'écriture.
    /// </summary>
    /// <param name="utilisateur">Utilisateur connecté.</param>
    /// <param name="controleur">Nom du contrôleur.</param>
    /// <param name="action">Nom de l'action.</param>
    /// <param name="requeteLecture">true pour une requête GET ou HEAD.</param>
    public static bool Autorise(ClaimsPrincipal utilisateur, string? controleur, string? action, bool requeteLecture)
    {
        var ecriture = !requeteLecture || (action != null && ActionsEcriture.Contains(action));
        return ecriture ? PeutEcrire(utilisateur, controleur) : PeutLire(utilisateur, controleur);
    }
}

/// <summary>
/// Appliqué à toutes les actions (Program.cs) ; l'authentification est déjà exigée par la politique par défaut
/// </summary>
/// <param name="journal">Logs techniques, pour tracer les accès refusés.</param>
public class DroitsFilter(ILogger<DroitsFilter> journal) : IAuthorizationFilter
{
    /// <summary>
    /// Redirige vers le changement de mot de passe s'il est provisoire, et renvoie 403 si l'action n'est pas
    /// autorisée.
    /// </summary>
    /// <param name="context">Contexte d'autorisation de la requête.</param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var utilisateur = context.HttpContext.User;
        if (utilisateur.Identity?.IsAuthenticated != true) return;

        var controleur = context.RouteData.Values["controller"] as string;
        var action = context.RouteData.Values["action"] as string;

        // Mot de passe provisoire : rien d'autre n'est accessible avant de l'avoir changé
        if (utilisateur.HasClaim(c => c.Type == Droits.ClaimDoitChanger) && controleur is not ("Connexion" or null))
        {
            context.Result = new RedirectToActionResult("MotDePasse", "Connexion", null);
            return;
        }

        var lecture = HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method);
        if (!Droits.Autorise(utilisateur, controleur, action, lecture))
        {
            journal.LogWarning("Accès refusé : {Identifiant} sur {Methode} {Controleur}/{Action}",
                utilisateur.FindFirstValue(ClaimTypes.Email), context.HttpContext.Request.Method, controleur, action);
            context.Result = new ForbidResult();
        }
    }
}

/// <summary>Masque les liens et formulaires vers des actions que l'utilisateur n'a pas le droit d'exécuter</summary>
[HtmlTargetElement("a", Attributes = "asp-action")]
[HtmlTargetElement("form", Attributes = "asp-action")]
public class DroitsTagHelper : TagHelper
{
    /// <summary>Contexte de la vue, fourni par Razor.</summary>
    [Microsoft.AspNetCore.Mvc.ViewFeatures.ViewContext, HtmlAttributeNotBound]
    public Microsoft.AspNetCore.Mvc.Rendering.ViewContext ViewContext { get; set; } = default!;

    /// <summary>Après les tag helpers standards</summary>
    public override int Order => 1000;

    /// <summary>
    /// Supprime la balise si l'action visée n'est pas autorisée ; un formulaire en GET reste une lecture.
    /// </summary>
    /// <param name="context">Contexte du tag helper.</param>
    /// <param name="output">Balise produite.</param>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var action = context.AllAttributes["asp-action"]?.Value?.ToString();
        var controleur = context.AllAttributes["asp-controller"]?.Value?.ToString()
            ?? ViewContext.RouteData.Values["controller"] as string;

        // Un formulaire en GET reste une lecture ; un formulaire sans méthode explicite est envoyé en POST par asp-action
        var methode = context.AllAttributes["method"]?.Value?.ToString();
        var lecture = context.TagName == "a" || string.Equals(methode, "get", StringComparison.OrdinalIgnoreCase);

        if (!Droits.Autorise(ViewContext.HttpContext.User, controleur, action, lecture))
            output.SuppressOutput();
    }
}

/// <summary>Politique, génération et réinitialisation des mots de passe des comptes.</summary>
public static class MotsDePasse
{
    /// <summary>Longueur minimale d'un mot de passe.</summary>
    public const int LongueurMinimale = 10;

    private const string Alphabet = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Null si le mot de passe convient, sinon le message à afficher</summary>
    /// <param name="motDePasse">Mot de passe proposé.</param>
    public static string? Verifier(string? motDePasse)
    {
        if (string.IsNullOrEmpty(motDePasse) || motDePasse.Length < LongueurMinimale)
            return $"Le mot de passe doit contenir au moins {LongueurMinimale} caractères.";
        if (!motDePasse.Any(char.IsLetter) || !motDePasse.Any(char.IsDigit))
            return "Le mot de passe doit contenir au moins une lettre et un chiffre.";
        return null;
    }

    /// <summary>
    /// Pose un mot de passe provisoire, débloque le compte et ferme ses sessions ; renvoie le provisoire, à
    /// transmettre une seule fois
    /// </summary>
    /// <param name="compte">Compte à réinitialiser (modifié en mémoire, à enregistrer).</param>
    public static string Reinitialiser(Compte compte)
    {
        var provisoire = Generer();
        compte.MotDePasseHash = new PasswordHasher<Compte>().HashPassword(compte, provisoire);
        compte.DoitChangerMotDePasse = true;
        compte.EchecsConnexion = 0;
        compte.VerrouilleJusquA = null;
        compte.Jeton = Guid.NewGuid().ToString();
        return provisoire;
    }

    /// <summary>
    /// Mot de passe provisoire, sans caractères ambigus (l, 1, O, 0) car il est souvent recopié ou dicté
    /// </summary>
    public static string Generer()
    {
        while (true)
        {
            var candidat = RandomNumberGenerator.GetString(Alphabet, 14);
            if (Verifier(candidat) == null) return candidat;
        }
    }
}
