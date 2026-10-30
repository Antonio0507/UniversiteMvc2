using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

// Socle commun des contrôleurs : accès aux données, messages, enregistrement et suppression sécurisés
public abstract class AppController : Controller
{
    // Codes d'erreur MySQL / MariaDB
    private const int Doublon = 1062;
    private const int LigneReferencee = 1451;
    private const int ReferenceIntrouvable = 1452;

    protected readonly ApplicationDbContext _context;

    protected AppController(ApplicationDbContext context)
    {
        _context = context;   // injecté par ASP.NET Core
    }

    protected void Succes(string message) => TempData["Succes"] = message;

    protected void Erreur(string message) => TempData["Erreur"] = message;

    // Reste affiché jusqu'à fermeture manuelle (mot de passe provisoire à recopier…)
    protected void Information(string message) => TempData["Information"] = message;

    // Enregistre les changements ; une valeur en double (index unique) devient une erreur de formulaire
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

    protected ViewResult ConfirmerSuppression(string titre, string libelle, int id,
        IEnumerable<string>? blocages = null, IEnumerable<string>? effets = null) =>
        View("Supprimer", new Suppression(titre, libelle, id, blocages?.ToList() ?? [], effets?.ToList() ?? []));

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

    // Ne garde que les messages dont le compteur est non nul
    protected static IEnumerable<string> Si(params (int Nombre, string Message)[] lignes) =>
        lignes.Where(l => l.Nombre > 0).Select(l => l.Message);
}
