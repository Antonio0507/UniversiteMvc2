using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

/// <summary>
/// Socle commun des contrôleurs : accès aux données, messages, enregistrement et suppression sécurisés
/// </summary>
public abstract class AppController : Controller
{
    // Codes d'erreur MySQL / MariaDB
    private const int Doublon = 1062;
    private const int LigneReferencee = 1451;
    private const int ReferenceIntrouvable = 1452;

    /// <summary>Contexte de base de données de la requête.</summary>
    protected readonly ApplicationDbContext _context;

    /// <summary>Initialise le socle commun.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    protected AppController(ApplicationDbContext context)
    {
        _context = context;   // injecté par ASP.NET Core
    }

    /// <summary>Message de confirmation affiché après la prochaine redirection, puis effacé automatiquement.</summary>
    /// <param name="message">Texte affiché.</param>
    protected void Succes(string message) => TempData["Succes"] = message;

    /// <summary>Message d'erreur affiché après la prochaine redirection.</summary>
    /// <param name="message">Texte affiché.</param>
    protected void Erreur(string message) => TempData["Erreur"] = message;

    /// <summary>Reste affiché jusqu'à fermeture manuelle (mot de passe provisoire à recopier…)</summary>
    /// <param name="message">Texte affiché.</param>
    protected void Information(string message) => TempData["Information"] = message;

    /// <summary>
    /// Enregistre les changements ; une valeur en double (index unique) devient une erreur de formulaire
    /// </summary>
    /// <returns>true si l'enregistrement a réussi ; false si une erreur a été ajoutée au formulaire.</returns>
    protected async Task<bool> EnregistrerAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: Doublon })
        {
            ModelState.AddModelError(string.Empty, "Enregistrement refusé : un code, un e-mail ou un matricule identique existe déjà.");
            return false;
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: ReferenceIntrouvable })
        {
            // Liste déroulante vide ou élément supprimé entre-temps : la clé étrangère ne pointe sur rien
            ModelState.AddModelError(string.Empty, "Enregistrement refusé : un élément choisi dans une liste n'existe pas (ou plus).");
            return false;
        }
    }

    /// <summary>Affiche la page de confirmation de suppression partagée par tous les modules.</summary>
    /// <param name="titre">Titre de la page.</param>
    /// <param name="libelle">Nom de l'élément à supprimer.</param>
    /// <param name="id">Identifiant de l'élément.</param>
    /// <param name="blocages">Raisons qui empêchent la suppression ; la page n'offre alors pas de bouton.</param>
    /// <param name="effets">Conséquences de la suppression (éléments supprimés ou détachés avec).</param>
    protected ViewResult ConfirmerSuppression(string titre, string libelle, int id,
        IEnumerable<string>? blocages = null, IEnumerable<string>? effets = null) =>
        View("Supprimer", new Suppression(titre, libelle, id, blocages?.ToList() ?? [], effets?.ToList() ?? []));

    /// <summary>
    /// Supprime l'élément ; si des enregistrements en dépendent encore, affiche une erreur au lieu de planter.
    /// Revient ensuite à la liste.
    /// </summary>
    /// <param name="entite">Élément à supprimer.</param>
    /// <param name="confirmation">Message affiché si la suppression réussit.</param>
    protected async Task<IActionResult> SupprimerAsync(object entite, string confirmation)
    {
        try
        {
            _context.Remove(entite);
            await _context.SaveChangesAsync();
            Succes(confirmation);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: LigneReferencee })
        {
            // OnDelete(Restrict) : des enregistrements dépendent encore de celui-ci
            Erreur("Suppression impossible : des enregistrements y sont encore rattachés.");
        }
        return RedirectToAction("Index");
    }

    /// <summary>Ne garde que les messages dont le compteur est non nul</summary>
    /// <param name="lignes">Paires (compteur, message).</param>
    protected static IEnumerable<string> Si(params (int Nombre, string Message)[] lignes) =>
        lignes.Where(l => l.Nombre > 0).Select(l => l.Message);
}
