using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Razor.TagHelpers;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure;

// Règles d'accès, déduites du sommaire (Navigation) : une seule source pour le contrôle serveur et l'affichage des liens
public static class Droits
{
    public const string ClaimDoitChanger = "doit_changer_mot_de_passe";
    public const string ClaimJeton = "jeton";

    // Actions GET qui ouvrent une écriture, ou sortent des données en masse
    private static readonly HashSet<string> ActionsEcriture = new(StringComparer.OrdinalIgnoreCase)
    {
        "Create", "Edit", "Delete", "Exporter", "ExporterImpayes"
    };

    public static bool PeutLire(ClaimsPrincipal utilisateur, string? controleur)
    {
        var section = Navigation.Par(controleur);
        return section == null || !section.Reserve || utilisateur.IsInRole(Roles.Administrateur);
    }

    public static bool PeutEcrire(ClaimsPrincipal utilisateur, string? controleur)
    {
        var section = Navigation.Par(controleur);
        if (section == null) return true;
        if (utilisateur.IsInRole(Roles.Administrateur)) return true;
        if (section.Reserve) return false;
        return section.Ecriture == null || section.Ecriture.Any(utilisateur.IsInRole);
    }

    public static bool Autorise(ClaimsPrincipal utilisateur, string? controleur, string? action, bool requeteLecture)
    {
        var ecriture = !requeteLecture || (action != null && ActionsEcriture.Contains(action));
        return ecriture ? PeutEcrire(utilisateur, controleur) : PeutLire(utilisateur, controleur);
    }
}

// Appliqué à toutes les actions (Program.cs) ; l'authentification est déjà exigée par la politique par défaut
public class DroitsFilter : IAuthorizationFilter
{
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
            context.Result = new ForbidResult();
    }
}

// Masque les liens et formulaires vers des actions que l'utilisateur n'a pas le droit d'exécuter
[HtmlTargetElement("a", Attributes = "asp-action")]
[HtmlTargetElement("form", Attributes = "asp-action")]
public class DroitsTagHelper : TagHelper
{
    [Microsoft.AspNetCore.Mvc.ViewFeatures.ViewContext, HtmlAttributeNotBound]
    public Microsoft.AspNetCore.Mvc.Rendering.ViewContext ViewContext { get; set; } = default!;

    // Après les tag helpers standards
    public override int Order => 1000;

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

public static class MotsDePasse
{
    public const int LongueurMinimale = 10;

    private const string Alphabet = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    // Null si le mot de passe convient, sinon le message à afficher
    public static string? Verifier(string? motDePasse)
    {
        if (string.IsNullOrEmpty(motDePasse) || motDePasse.Length < LongueurMinimale)
            return $"Le mot de passe doit contenir au moins {LongueurMinimale} caractères.";
        if (!motDePasse.Any(char.IsLetter) || !motDePasse.Any(char.IsDigit))
            return "Le mot de passe doit contenir au moins une lettre et un chiffre.";
        return null;
    }

    // Mot de passe provisoire, sans caractères ambigus (l, 1, O, 0) car il est souvent recopié ou dicté
    public static string Generer()
    {
        while (true)
        {
            var candidat = RandomNumberGenerator.GetString(Alphabet, 14);
            if (Verifier(candidat) == null) return candidat;
        }
    }
}
