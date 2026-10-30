using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Controllers;

// Consultation du journal d'audit (administrateurs uniquement, voir Navigation) ; il n'est jamais modifiable
public class JournalController : AppController
{
    public JournalController(ApplicationDbContext context) : base(context) { }

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var requete = _context.JournalAudit.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            requete = requete.Where(j => j.Auteur.Contains(q) || j.Action.Contains(q) || j.Entite.Contains(q) || (j.Details != null && j.Details.Contains(q)));
        }

        ViewData["q"] = q;
        return View(await ListePaginee<JournalAudit>.CreerAsync(requete.OrderByDescending(j => j.Id), page, 50));
    }
}
