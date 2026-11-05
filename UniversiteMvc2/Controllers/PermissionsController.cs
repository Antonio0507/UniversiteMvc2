using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

/// <summary>
/// Matrice des permissions (administrateurs uniquement, voir Navigation) : pour chaque profil du personnel et chaque
/// section, aucun accès, lecture ou écriture. L'administrateur garde tous les droits ; les sections d'administration
/// ne se délèguent pas ; un étudiant n'accède qu'à son espace.
/// </summary>
public class PermissionsController : AppController
{
    private readonly MatriceDroits _matrice;

    /// <summary>Crée le contrôleur.</summary>
    /// <param name="context">Contexte de base de données, injecté par ASP.NET Core.</param>
    /// <param name="matrice">Matrice des permissions, partagée par toute l'application.</param>
    public PermissionsController(ApplicationDbContext context, MatriceDroits matrice) : base(context)
    {
        _matrice = matrice;
    }

    /// <summary>Nom du champ de formulaire d'une case de la matrice.</summary>
    /// <param name="role">Profil.</param>
    /// <param name="section">Section.</param>
    public static string Champ(string role, Section section) => $"{role}|{section.Controleur}";

    /// <summary>Affiche la matrice : une ligne par section, une colonne par profil.</summary>
    public async Task<IActionResult> Index() => View(await _matrice.ChargerAsync(_context));

    /// <summary>
    /// Enregistre la matrice. Les changements s'appliquent dès la requête suivante de chaque utilisateur connecté,
    /// et sont tracés dans le journal d'audit.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(IFormCollection formulaire)
    {
        var lignes = await _context.Permissions.ToListAsync();
        foreach (var role in Roles.Configurables)
        {
            foreach (var section in MatriceDroits.Sections)
            {
                if (!Enum.TryParse<Acces>(formulaire[Champ(role, section)].ToString(), out var acces) || !Enum.IsDefined(acces))
                    return BadRequest();

                var ligne = lignes.FirstOrDefault(l => l.Role == role && l.Section == section.Controleur);
                if (ligne == null)
                    _context.Permissions.Add(new Permission { Role = role, Section = section.Controleur, Acces = acces });
                else
                    ligne.Acces = acces;
            }
        }

        await _context.SaveChangesAsync();
        _matrice.Invalider();
        Succes("Permissions enregistrées : elles s'appliquent dès maintenant.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Supprime tous les réglages : chaque profil retrouve ses droits par défaut.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reinitialiser()
    {
        _context.Permissions.RemoveRange(await _context.Permissions.ToListAsync());
        await _context.SaveChangesAsync();
        _matrice.Invalider();
        Succes("Permissions par défaut rétablies.");
        return RedirectToAction(nameof(Index));
    }
}
