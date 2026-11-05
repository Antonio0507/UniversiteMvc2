using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure;

/// <summary>
/// Règles d'accès : le sommaire (Navigation) liste les sections, la matrice des permissions (MatriceDroits) dit qui
/// y accède. Une seule source pour le contrôle serveur et l'affichage des liens.
/// </summary>
public static class Droits
{
    /// <summary>Claim posé tant que le compte doit changer son mot de passe provisoire.</summary>
    public const string ClaimDoitChanger = "doit_changer_mot_de_passe";
    /// <summary>
    /// Claim qui porte le jeton de session du compte ; s'il ne correspond plus à la base, la session est rejetée.
    /// </summary>
    public const string ClaimJeton = "jeton";
    /// <summary>
    /// Claim « Section=niveau » ajouté à chaque requête à partir de la matrice des permissions (jamais stocké dans le
    /// cookie) : une permission modifiée s'applique dès la requête suivante.
    /// </summary>
    public const string ClaimAcces = "acces";

    // Actions GET qui ouvrent une écriture, ou sortent des données en masse
    private static readonly HashSet<string> ActionsEcriture = new(StringComparer.OrdinalIgnoreCase)
    {
        "Create", "Edit", "Delete", "Exporter", "ExporterImpayes"
    };

    /// <summary>Claims d'accès correspondant à une ligne de la matrice des permissions.</summary>
    /// <param name="acces">Niveau d'accès par section (nom du contrôleur).</param>
    public static IEnumerable<Claim> Claims(IReadOnlyDictionary<string, Acces> acces) =>
        acces.Select(a => new Claim(ClaimAcces, $"{a.Key}={(int)a.Value}"));

    /// <summary>
    /// Niveau d'accès de l'utilisateur à une section : écriture pour l'administrateur ; aucun pour les autres sur une
    /// section réservée ; sinon celui de la matrice des permissions.
    /// </summary>
    /// <param name="utilisateur">Utilisateur connecté.</param>
    /// <param name="section">Section du sommaire.</param>
    public static Acces Niveau(ClaimsPrincipal utilisateur, Section section)
    {
        if (utilisateur.IsInRole(Roles.Administrateur)) return Acces.Ecriture;
        if (section.Reserve) return Acces.Aucun;

        var prefixe = section.Controleur + "=";
        var claim = utilisateur.FindFirst(c => c.Type == ClaimAcces && c.Value.StartsWith(prefixe, StringComparison.Ordinal));
        return claim != null && int.TryParse(claim.Value[prefixe.Length..], out var n) && Enum.IsDefined((Acces)n) ? (Acces)n : Acces.Aucun;
    }

    /// <summary>
    /// Indique si l'utilisateur peut consulter une section. Hors sommaire (tableau de bord, connexion…), la réponse
    /// est oui : ces pages se protègent elles-mêmes.
    /// </summary>
    /// <param name="utilisateur">Utilisateur connecté.</param>
    /// <param name="controleur">Nom du contrôleur.</param>
    public static bool PeutLire(ClaimsPrincipal utilisateur, string? controleur)
    {
        var section = Navigation.Par(controleur);
        return section == null || Niveau(utilisateur, section) >= Acces.Lecture;
    }

    /// <summary>Indique si l'utilisateur peut modifier une section.</summary>
    /// <param name="utilisateur">Utilisateur connecté.</param>
    /// <param name="controleur">Nom du contrôleur.</param>
    public static bool PeutEcrire(ClaimsPrincipal utilisateur, string? controleur)
    {
        var section = Navigation.Par(controleur);
        return section == null || Niveau(utilisateur, section) == Acces.Ecriture;
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
/// Matrice des permissions : niveau d'accès de chaque profil du personnel à chaque section non réservée. Lue en base
/// (table Permissions) et gardée en mémoire jusqu'à la prochaine modification ; une case sans ligne en base prend sa
/// valeur par défaut.
/// </summary>
public class MatriceDroits
{
    private static readonly IReadOnlyDictionary<string, Acces> Vide = new Dictionary<string, Acces>();

    private volatile IReadOnlyDictionary<string, IReadOnlyDictionary<string, Acces>>? _cache;

    /// <summary>Sections dont l'administrateur règle l'accès : toutes, sauf les sections réservées.</summary>
    public static IEnumerable<Section> Sections => Navigation.Sections.Where(s => !s.Reserve);

    /// <summary>
    /// Valeur par défaut d'une case : écriture si la section le prévoit pour ce profil (Section.Ecriture), lecture
    /// sinon ; aucun accès pour un profil non configurable (Étudiant) ou une section réservée.
    /// </summary>
    /// <param name="role">Profil.</param>
    /// <param name="section">Section.</param>
    public static Acces ParDefaut(string role, Section section)
    {
        if (section.Reserve || !Roles.Configurables.Contains(role)) return Acces.Aucun;
        return section.Ecriture == null || section.Ecriture.Contains(role) ? Acces.Ecriture : Acces.Lecture;
    }

    /// <summary>Ligne par défaut d'un profil : section → niveau d'accès.</summary>
    /// <param name="role">Profil.</param>
    public static IReadOnlyDictionary<string, Acces> ParDefaut(string role) =>
        Sections.ToDictionary(s => s.Controleur, s => ParDefaut(role, s));

    /// <summary>Matrice complète : profil configurable → section → niveau d'accès.</summary>
    /// <param name="db">Contexte de base de données.</param>
    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, Acces>>> ChargerAsync(ApplicationDbContext db)
    {
        if (_cache is { } cache) return cache;

        var lignes = await db.Permissions.AsNoTracking().ToListAsync();
        var matrice = Roles.Configurables.ToDictionary(
            role => role,
            role => (IReadOnlyDictionary<string, Acces>)Sections.ToDictionary(
                s => s.Controleur,
                s => lignes.FirstOrDefault(l => l.Role == role && l.Section == s.Controleur)?.Acces ?? ParDefaut(role, s)));
        _cache = matrice;
        return matrice;
    }

    /// <summary>Ligne d'un profil ; vide (aucun accès) pour un profil non configurable.</summary>
    /// <param name="db">Contexte de base de données.</param>
    /// <param name="role">Profil.</param>
    public async Task<IReadOnlyDictionary<string, Acces>> PourAsync(ApplicationDbContext db, string role) =>
        (await ChargerAsync(db)).TryGetValue(role, out var ligne) ? ligne : Vide;

    /// <summary>À appeler après chaque modification de la table Permissions.</summary>
    public void Invalider() => _cache = null;
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
