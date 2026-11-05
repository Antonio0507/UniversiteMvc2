using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure;

/// <summary>Ouverture et contrôle des sessions (cookie de connexion)</summary>
public static class Sessions
{
    /// <summary>
    /// Ouvre la session d'un compte : identifiant, nom, e-mail, profil et jeton de session. Les droits ne sont pas
    /// stockés dans le cookie, ils sont relus à chaque requête (voir ValiderAsync).
    /// </summary>
    /// <param name="http">Requête en cours.</param>
    /// <param name="compte">Compte qui se connecte.</param>
    public static Task OuvrirAsync(HttpContext http, Compte compte)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, compte.Id.ToString()),
            new(ClaimTypes.Name, compte.Nom),
            new(ClaimTypes.Email, compte.Email),
            new(ClaimTypes.Role, compte.Role),
            new(Droits.ClaimJeton, compte.Jeton)
        };
        if (compte.DoitChangerMotDePasse) claims.Add(new Claim(Droits.ClaimDoitChanger, "1"));

        var identite = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identite));
    }

    /// <summary>
    /// Appelé à chaque requête authentifiée : rejette la session si le compte est désactivé ou si son jeton a changé
    /// (mot de passe, profil ou état modifiés), sinon ajoute les droits de son profil, lus dans la matrice des
    /// permissions.
    /// </summary>
    /// <param name="contexte">Contexte de validation du cookie.</param>
    public static async Task ValiderAsync(CookieValidatePrincipalContext contexte)
    {
        var services = contexte.HttpContext.RequestServices;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var principal = contexte.Principal!;
        var id = int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var n) ? n : 0;
        var jeton = principal.FindFirstValue(Droits.ClaimJeton);

        var role = await db.Comptes.Where(c => c.Id == id && c.Actif && c.Jeton == jeton).Select(c => c.Role).FirstOrDefaultAsync();
        if (role == null)
        {
            contexte.RejectPrincipal();
            await contexte.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        // Les droits d'un éventuel cookie réémis sont ignorés : seule la matrice actuelle compte
        var acces = await services.GetRequiredService<MatriceDroits>().PourAsync(db, role);
        var claims = principal.Claims.Where(c => c.Type != Droits.ClaimAcces).Concat(Droits.Claims(acces));
        var identite = new ClaimsIdentity(claims, principal.Identity!.AuthenticationType);
        contexte.ReplacePrincipal(new ClaimsPrincipal(identite));
    }
}
