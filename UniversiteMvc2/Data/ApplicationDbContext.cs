using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Data;

/// <summary>
/// Accès à la base MySQL/MariaDB par Entity Framework Core : tables, index, relations et règles de suppression.
/// Chaque écriture est tracée dans le journal d'audit.
/// </summary>
public class ApplicationDbContext : DbContext
{
    private readonly IHttpContextAccessor? _http;

    /// <summary>Crée le contexte.</summary>
    /// <param name="options">Connexion et fournisseur MySQL, configurés dans Program.cs.</param>
    /// <param name="http">Requête en cours, pour connaître l'auteur d'une écriture ; absent hors requête (démarrage, ligne de commande).</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor? http = null)
        : base(options)
    {
        _http = http;
    }

    /// <summary>Étudiants et enseignants, stockés dans une seule table (héritage TPH, colonne Role).</summary>
    public DbSet<Utilisateur> Utilisateurs { get; set; }
    /// <summary>Table des étudiants (partie de Utilisateurs).</summary>
    public DbSet<Etudiant> Etudiants { get; set; }
    /// <summary>Table des enseignants (partie de Utilisateurs).</summary>
    public DbSet<Enseignant> Enseignants { get; set; }
    /// <summary>Table des filières.</summary>
    public DbSet<Filiere> Filieres { get; set; }
    /// <summary>Table des niveaux.</summary>
    public DbSet<Niveau> Niveaux { get; set; }
    /// <summary>Table des années académiques.</summary>
    public DbSet<AnneeAcad> AnneesAcad { get; set; }
    /// <summary>Table des unités d'enseignement.</summary>
    public DbSet<UE> UEs { get; set; }
    /// <summary>Table des cours.</summary>
    public DbSet<Cours> Cours { get; set; }
    /// <summary>Table des inscriptions.</summary>
    public DbSet<Inscription> Inscriptions { get; set; }
    /// <summary>Table des paiements.</summary>
    public DbSet<Paiement> Paiements { get; set; }
    /// <summary>Table des tarifs.</summary>
    public DbSet<Tarif> Tarifs { get; set; }
    /// <summary>Table des notes.</summary>
    public DbSet<Note> Notes { get; set; }
    /// <summary>Table des salles.</summary>
    public DbSet<Salle> Salles { get; set; }
    /// <summary>Table des séances de l'emploi du temps.</summary>
    public DbSet<Seance> Seances { get; set; }
    /// <summary>Table des absences.</summary>
    public DbSet<Absence> Absences { get; set; }
    /// <summary>Table des bourses, exonérations et réductions.</summary>
    public DbSet<Aide> Aides { get; set; }
    /// <summary>Table des comptes de connexion (personnel et étudiants).</summary>
    public DbSet<Compte> Comptes { get; set; }
    /// <summary>Permissions des profils du personnel, réglées par l'administrateur.</summary>
    public DbSet<Permission> Permissions { get; set; }
    /// <summary>Journal d'audit : une ligne par écriture ou événement de sécurité.</summary>
    public DbSet<JournalAudit> JournalAudit { get; set; }

    /// <summary>Déclare l'héritage, les index uniques, les relations et leur comportement à la suppression.</summary>
    /// <param name="modelBuilder">Constructeur du modèle EF Core.</param>
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

        // Emploi du temps et assiduité : une séance garde son historique d'appel ; supprimer la séance supprime son appel
        modelBuilder.Entity<Salle>().HasIndex(s => s.Code).IsUnique();
        modelBuilder.Entity<Seance>().HasIndex(s => s.Date);
        modelBuilder.Entity<Absence>().HasIndex(a => new { a.SeanceId, a.InscriptionId }).IsUnique();
        modelBuilder.Entity<Seance>().HasOne(s => s.Cours).WithMany(c => c.Seances).HasForeignKey(s => s.CoursId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Seance>().HasOne(s => s.Salle).WithMany(s => s.Seances).HasForeignKey(s => s.SalleId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Absence>().HasOne(a => a.Seance).WithMany(s => s.Absences).HasForeignKey(a => a.SeanceId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Absence>().HasOne(a => a.Inscription).WithMany(i => i.Absences).HasForeignKey(a => a.InscriptionId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Aide>().HasOne(a => a.Inscription).WithMany(i => i.Aides).HasForeignKey(a => a.InscriptionId).OnDelete(DeleteBehavior.Restrict);

        // Un compte par étudiant au plus ; un dossier qui a un compte ne se supprime pas
        modelBuilder.Entity<Compte>().HasIndex(c => c.EtudiantId).IsUnique();
        modelBuilder.Entity<Compte>().HasOne(c => c.Etudiant).WithMany().HasForeignKey(c => c.EtudiantId).OnDelete(DeleteBehavior.Restrict);

        // Une case de la matrice des permissions par profil et par section
        modelBuilder.Entity<Permission>().HasIndex(p => new { p.Role, p.Section }).IsUnique();
    }

    // --- Journal d'audit ----------------------------------------------------

    // Colonnes techniques de Compte : leurs variations ne méritent pas une ligne de journal
    private static readonly HashSet<string> Ignorees = ["EchecsConnexion", "VerrouilleJusquA", "DerniereConnexion", "Jeton"];

    // Ni le contenu ni l'empreinte du mot de passe n'entrent dans le journal
    private const string MotDePasse = "MotDePasseHash";

    /// <summary>
    /// Toute écriture est tracée dans la même transaction : pas de modification sans trace, ni de trace sans
    /// modification
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">Paramètre standard d'EF Core.</param>
    /// <param name="cancellationToken">Annulation de l'opération.</param>
    /// <returns>Nombre de lignes écrites, hors journal d'audit.</returns>
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
