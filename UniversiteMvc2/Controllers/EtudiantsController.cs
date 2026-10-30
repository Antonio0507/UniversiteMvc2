using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

public class EtudiantsController : AppController
{
    public EtudiantsController(ApplicationDbContext context) : base(context) { }

    public async Task<IActionResult> Exporter(string? q)
    {
        var requete = _context.Etudiants.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            requete = requete.Where(e => e.Nom.Contains(q) || e.Prenom.Contains(q) || e.Matricule.Contains(q) || e.Email.Contains(q));
        }
        var etudiants = await requete.OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ToListAsync();
        var contenu = Csv.Generer(
            ["Matricule", "Nom", "Prénom", "Naissance", "E-mail", "Téléphone"],
            etudiants.Select(e => new object?[] { e.Matricule, e.Nom, e.Prenom, e.DateNaissance, e.Email, e.Telephone }));
        return File(contenu, "text/csv", "etudiants.csv");
    }

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var requete = _context.Etudiants.AsNoTracking()
            .Include(e => e.Inscriptions).ThenInclude(i => i.Filiere)
            .Include(e => e.Inscriptions).ThenInclude(i => i.Niveau)
            .Include(e => e.Inscriptions).ThenInclude(i => i.AnneeAcad)
            .AsSplitQuery();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            requete = requete.Where(e => e.Nom.Contains(q) || e.Prenom.Contains(q) || e.Matricule.Contains(q) || e.Email.Contains(q));
        }

        ViewData["q"] = q;
        return View(await ListePaginee<Etudiant>.CreerAsync(requete.OrderBy(e => e.Nom).ThenBy(e => e.Prenom), page));
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var etudiant = await _context.Etudiants.AsNoTracking()
            .Include(e => e.Inscriptions).ThenInclude(i => i.Filiere)
            .Include(e => e.Inscriptions).ThenInclude(i => i.Niveau)
            .Include(e => e.Inscriptions).ThenInclude(i => i.AnneeAcad)
            .Include(e => e.Inscriptions).ThenInclude(i => i.Paiements)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id);
        if (etudiant == null) return NotFound();

        return View(etudiant);
    }

    public async Task<IActionResult> Create() =>
        View("Formulaire", new Etudiant { Matricule = await ProchainMatriculeAsync(), DateNaissance = new DateTime(2005, 1, 1) });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Nom,Prenom,Email,Telephone,Matricule,DateNaissance")] Etudiant etudiant)
    {
        if (ModelState.IsValid)
        {
            _context.Add(etudiant);
            if (await EnregistrerAsync())
            {
                Succes($"Dossier de {etudiant.NomComplet} créé.");
                return RedirectToAction(nameof(Details), new { id = etudiant.Id });
            }
        }
        return View("Formulaire", etudiant);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var etudiant = await _context.Etudiants.FindAsync(id);
        if (etudiant == null) return NotFound();

        return View("Formulaire", etudiant);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id)
    {
        var etudiant = await _context.Etudiants.FindAsync(id);
        if (etudiant == null) return NotFound();

        if (await TryUpdateModelAsync(etudiant, string.Empty,
                e => e.Nom, e => e.Prenom, e => e.Email, e => e.Telephone, e => e.Matricule, e => e.DateNaissance)
            && await EnregistrerAsync())
        {
            Succes("Dossier mis à jour.");
            return RedirectToAction(nameof(Details), new { id });
        }
        return View("Formulaire", etudiant);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var etudiant = await _context.Etudiants.FindAsync(id);
        if (etudiant == null) return NotFound();

        var inscriptions = await _context.Inscriptions.CountAsync(i => i.EtudiantId == id);
        return ConfirmerSuppression("Supprimer l'étudiant", $"{etudiant.NomComplet} — {etudiant.Matricule}", etudiant.Id,
            blocages: Si((inscriptions, $"{Formats.Pluriel(inscriptions, "inscription rattachée", "inscriptions rattachées")} à ce dossier.")));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var etudiant = await _context.Etudiants.FindAsync(id);
        if (etudiant == null) return RedirectToAction(nameof(Index));

        return await SupprimerAsync(etudiant, $"Dossier de {etudiant.NomComplet} supprimé.");
    }

    // Matricule suivant de l'année : ETU2026001, ETU2026002…
    private async Task<string> ProchainMatriculeAsync()
    {
        var prefixe = $"ETU{DateTime.Today.Year}";
        var existants = await _context.Etudiants
            .Where(e => e.Matricule.StartsWith(prefixe))
            .Select(e => e.Matricule)
            .ToListAsync();
        var dernier = existants
            .Select(m => int.TryParse(m[prefixe.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefixe}{dernier + 1:000}";
    }
}
