using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Data;

public class ApplicationDbContext : DbContext
{
    private readonly IHttpContextAccessor? _http;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor? http = null)
        : base(options)
    {
        _http = http;
    }

    public DbSet<Utilisateur> Utilisateurs { get; set; }
    public DbSet<Etudiant> Etudiants { get; set; }
    public DbSet<Enseignant> Enseignants { get; set; }
    public DbSet<Filiere> Filieres { get; set; }
    public DbSet<Niveau> Niveaux { get; set; }
    public DbSet<AnneeAcad> AnneesAcad { get; set; }
    public DbSet<UE> UEs { get; set; }
    public DbSet<Cours> Cours { get; set; }
    public DbSet<Inscription> Inscriptions { get; set; }
    public DbSet<Paiement> Paiements { get; set; }
    public DbSet<Tarif> Tarifs { get; set; }
    public DbSet<Note> Notes { get; set; }
    public DbSet<Compte> Comptes { get; set; }
    public DbSet<JournalAudit> JournalAudit { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Héritage TPH : une seule table Utilisateurs, discriminateur sur la colonne Role
        modelBuilder.Entity<Utilisateur>()
            .HasDiscriminator<string>("Role")
            .HasValue<Etudiant>("Etudiant")
            .HasValue<Enseignant>("Enseignant");

        modelBuilder.Entity<Utilisateur>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<Etudiant>().HasIndex(e => e.Matricule).IsUnique();
        modelBuilder.Entity<Filiere>().HasIndex(f => f.Code).IsUnique();
        modelBuilder.Entity<Niveau>().HasIndex(n => n.Code).IsUnique();
        modelBuilder.Entity<UE>().HasIndex(u => u.Code).IsUnique();
        modelBuilder.Entity<AnneeAcad>().HasIndex(a => a.Libelle).IsUnique();
        modelBuilder.Entity<Compte>().HasIndex(c => c.Email).IsUnique();
        modelBuilder.Entity<JournalAudit>().HasIndex(j => j.DateUtc);

        // Un étudiant, une inscription par année ; une note par UE et par inscription ; un tarif par cursus et par année
        modelBuilder.Entity<Inscription>().HasIndex(i => new { i.EtudiantId, i.AnneeAcadId }).IsUnique();
        modelBuilder.Entity<Note>().HasIndex(n => new { n.InscriptionId, n.UEId }).IsUnique();
        modelBuilder.Entity<Tarif>().HasIndex(t => new { t.AnneeAcadId, t.FiliereId, t.NiveauId }).IsUnique();

        // Relations 1-N
        modelBuilder.Entity<Inscription>()
            .HasOne(i => i.Etudiant)
            .WithMany(e => e.Inscriptions)
            .HasForeignKey(i => i.EtudiantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Inscription>()
            .HasOne(i => i.Filiere)
            .WithMany(f => f.Inscriptions)
            .HasForeignKey(i => i.FiliereId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Inscription>()
            .HasOne(i => i.Niveau)
            .WithMany(n => n.Inscriptions)
            .HasForeignKey(i => i.NiveauId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Inscription>()
            .HasOne(i => i.AnneeAcad)
            .WithMany(a => a.Inscriptions)
            .HasForeignKey(i => i.AnneeAcadId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Paiement>()
            .HasOne(p => p.Inscription)
            .WithMany(i => i.Paiements)
            .HasForeignKey(p => p.InscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UE>()
            .HasOne(u => u.Filiere)
            .WithMany(f => f.UEs)
            .HasForeignKey(u => u.FiliereId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UE>()
            .HasOne(u => u.Niveau)
            .WithMany(n => n.UEs)
            .HasForeignKey(u => u.NiveauId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Cours>()
            .HasOne(c => c.UE)
            .WithMany(u => u.Cours)
            .HasForeignKey(c => c.UEId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Cours>()
            .HasOne(c => c.Enseignant)
            .WithMany(e => e.Cours)
            .HasForeignKey(c => c.EnseignantId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Note>()
            .HasOne(n => n.Inscription)
            .WithMany(i => i.Notes)
            .HasForeignKey(n => n.InscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Note>()
            .HasOne(n => n.UE)
            .WithMany()
            .HasForeignKey(n => n.UEId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tarif>().HasOne(t => t.AnneeAcad).WithMany().HasForeignKey(t => t.AnneeAcadId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Tarif>().HasOne(t => t.Filiere).WithMany().HasForeignKey(t => t.FiliereId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Tarif>().HasOne(t => t.Niveau).WithMany().HasForeignKey(t => t.NiveauId).OnDelete(DeleteBehavior.Restrict);
    }

    // --- Journal d'audit ----------------------------------------------------

    // Colonnes techniques de Compte : leurs variations ne méritent pas une ligne de journal
    private static readonly HashSet<string> Ignorees = ["EchecsConnexion", "VerrouilleJusquA", "DerniereConnexion", "Jeton"];

    // Ni le contenu ni l'empreinte du mot de passe n'entrent dans le journal
    private const string MotDePasse = "MotDePasseHash";

    // Toute écriture est tracée dans la même transaction : pas de modification sans trace, ni de trace sans modification
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        var suivis = ChangeTracker.Entries()
            .Where(e => e.Entity is not Models.JournalAudit && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => (Entree: e, Action: Libelle(e.State), Details: Decrire(e)))
            .Where(s => s.Entree.State != EntityState.Modified || s.Details.Length > 0)
            .ToList();

        if (suivis.Count == 0) return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        await using var transaction = Database.CurrentTransaction == null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;

        var resultat = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        var auteur = _http?.HttpContext?.User.FindFirstValue(ClaimTypes.Email) ?? "système";
        JournalAudit.AddRange(suivis.Select(s => new JournalAudit
        {
            Auteur = auteur,
            Action = s.Action,
            Entite = s.Entree.Metadata.ClrType.Name,
            EntiteId = s.Entree.Metadata.FindProperty("Id") == null ? null : s.Entree.Property("Id").CurrentValue as int?,
            Details = s.Details.Length <= 2000 ? s.Details : s.Details[..2000]
        }));
        await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return resultat;
    }

    private static string Libelle(EntityState etat) => etat switch
    {
        EntityState.Added => "Création",
        EntityState.Deleted => "Suppression",
        _ => "Modification"
    };

    private static string Decrire(EntityEntry entree)
    {
        var morceaux = new List<string>();
        foreach (var propriete in entree.Properties)
        {
            var nom = propriete.Metadata.Name;
            if (nom == "Id" || Ignorees.Contains(nom)) continue;

            if (entree.State == EntityState.Modified)
            {
                if (!propriete.IsModified || Equals(propriete.OriginalValue, propriete.CurrentValue)) continue;
                morceaux.Add(nom == MotDePasse ? "Mot de passe modifié" : $"{nom} : {Texte(propriete.OriginalValue)} → {Texte(propriete.CurrentValue)}");
            }
            else if (nom != MotDePasse)
            {
                var valeur = entree.State == EntityState.Deleted ? propriete.OriginalValue : propriete.CurrentValue;
                morceaux.Add($"{nom} = {Texte(valeur)}");
            }
        }
        return string.Join(" ; ", morceaux);
    }

    private static string Texte(object? valeur) => valeur switch
    {
        null => "∅",
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable nombre => nombre.ToString(null, CultureInfo.InvariantCulture),
        _ => valeur.ToString() ?? "∅"
    };
}
