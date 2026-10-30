using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

public class EnseignantsController : AppController
{
    public EnseignantsController(ApplicationDbContext context) : base(context) { }

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var requete = _context.Enseignants.AsNoTracking().Include(e => e.Cours).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            requete = requete.Where(e => e.Nom.Contains(q) || e.Prenom.Contains(q) || e.Email.Contains(q) || (e.Specialite != null && e.Specialite.Contains(q)));
        }

        ViewData["q"] = q;
        return View(await ListePaginee<Enseignant>.CreerAsync(requete.OrderBy(e => e.Nom).ThenBy(e => e.Prenom), page));
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var enseignant = await _context.Enseignants.AsNoTracking()
            .Include(e => e.Cours).ThenInclude(c => c.UE).ThenInclude(u => u!.Filiere)
            .Include(e => e.Cours).ThenInclude(c => c.UE).ThenInclude(u => u!.Niveau)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id);
        if (enseignant == null) return NotFound();

        return View(enseignant);
    }

    public IActionResult Create()
    {
        RemplirListes();
        return View("Formulaire", new Enseignant());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Nom,Prenom,Email,Telephone,Grade,Specialite")] Enseignant enseignant)
    {
        if (ModelState.IsValid)
        {
            _context.Add(enseignant);
            if (await EnregistrerAsync())
            {
                Succes($"{enseignant.NomComplet} ajouté au corps enseignant.");
                return RedirectToAction(nameof(Details), new { id = enseignant.Id });
            }
        }
        RemplirListes(enseignant);
        return View("Formulaire", enseignant);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var enseignant = await _context.Enseignants.FindAsync(id);
        if (enseignant == null) return NotFound();

        RemplirListes(enseignant);
        return View("Formulaire", enseignant);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id)
    {
        var enseignant = await _context.Enseignants.FindAsync(id);
        if (enseignant == null) return NotFound();

        if (await TryUpdateModelAsync(enseignant, string.Empty,
                e => e.Nom, e => e.Prenom, e => e.Email, e => e.Telephone, e => e.Grade, e => e.Specialite)
            && await EnregistrerAsync())
        {
            Succes("Fiche enseignant mise à jour.");
            return RedirectToAction(nameof(Details), new { id });
        }
        RemplirListes(enseignant);
        return View("Formulaire", enseignant);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var enseignant = await _context.Enseignants.FindAsync(id);
        if (enseignant == null) return NotFound();

        // OnDelete(SetNull) : ses cours restent, sans enseignant
        var cours = await _context.Cours.CountAsync(c => c.EnseignantId == id);
        return ConfirmerSuppression("Supprimer l'enseignant", enseignant.NomComplet, enseignant.Id,
            effets: Si((cours, $"{Formats.Pluriel(cours, "cours")} {(cours > 1 ? "deviendront" : "deviendra")} « non attribué »." )));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var enseignant = await _context.Enseignants.FindAsync(id);
        if (enseignant == null) return RedirectToAction(nameof(Index));

        return await SupprimerAsync(enseignant, $"{enseignant.NomComplet} retiré du corps enseignant.");
    }

    private void RemplirListes(Enseignant? enseignant = null)
    {
        // Un grade saisi hors liste reste proposé
        var grades = Enseignant.Grades.ToList();
        if (!string.IsNullOrEmpty(enseignant?.Grade) && !grades.Contains(enseignant.Grade)) grades.Add(enseignant.Grade);
        ViewData["Grade"] = new SelectList(grades, enseignant?.Grade);
    }
}
