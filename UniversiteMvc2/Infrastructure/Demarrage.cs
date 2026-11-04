using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniversiteMvc2.Data;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure;

/// <summary>
/// Préparation de la base au lancement : schéma, référentiel minimal, premier administrateur, démonstration
/// (facultative)
/// </summary>
public static class Demarrage
{
    /// <summary>
    /// Argument de ligne de commande qui réinitialise un mot de passe au lieu de démarrer le serveur web.
    /// </summary>
    public const string CommandeReinitialisation = "reinitialiser-mot-de-passe";

    private const string MigrationInitiale = "20261030101445_Initial";

    /// <summary>
    /// Dernier recours en ligne de commande, si plus aucun administrateur ne peut se connecter. Réservé à qui a accès
    /// au serveur ; tracé dans le journal d'audit. Renvoie null si le compte n'existe pas.
    /// </summary>
    /// <param name="services">Services de l'application.</param>
    /// <param name="email">Identifiant du compte.</param>
    /// <returns>Le mot de passe provisoire, ou null.</returns>
    public static async Task<string?> ReinitialiserMotDePasseAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var identifiant = email.Trim().ToLowerInvariant();
        var compte = await db.Comptes.FirstOrDefaultAsync(c => c.Email == identifiant);
        if (compte == null) return null;

        var provisoire = MotsDePasse.Reinitialiser(compte);
        compte.Actif = true;
        await db.SaveChangesAsync();
        return provisoire;
    }

    /// <summary>
    /// Applique les migrations, crée les niveaux L1 à M2 sur une base vide, le premier administrateur s'il n'existe
    /// aucun compte, et le jeu d'essai si demandé.
    /// </summary>
    /// <param name="services">Services de l'application.</param>
    public static async Task PreparerAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var fournisseur = scope.ServiceProvider;
        var db = fournisseur.GetRequiredService<ApplicationDbContext>();
        var configuration = fournisseur.GetRequiredService<IConfiguration>();
        var journal = fournisseur.GetRequiredService<ILoggerFactory>().CreateLogger("Demarrage");

        await ReconnaitreBaseExistanteAsync(db, journal);
        await db.Database.MigrateAsync();

        if (!await db.Niveaux.AnyAsync())
        {
            db.Niveaux.AddRange(
                new Niveau { Code = "L1", Libelle = "Licence 1" },
                new Niveau { Code = "L2", Libelle = "Licence 2" },
                new Niveau { Code = "L3", Libelle = "Licence 3" },
                new Niveau { Code = "M1", Libelle = "Master 1" },
                new Niveau { Code = "M2", Libelle = "Master 2" });
            await db.SaveChangesAsync();
        }

        await CreerPremierAdministrateurAsync(db, configuration, journal);

        if (configuration.GetValue<bool>("Demarrage:DonneesDemo"))
            await DonneesDemo.ChargerAsync(db);
    }

    // Une base créée par une ancienne version (EnsureCreated) a déjà le schéma initial, sans historique de migrations.
    // On l'inscrit comme appliquée, sinon la migration tenterait de recréer des tables existantes.
    private static async Task ReconnaitreBaseExistanteAsync(ApplicationDbContext db, ILogger journal)
    {
        if (!await db.Database.CanConnectAsync()) return;

        var tables = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.tables WHERE table_schema = DATABASE() AND LOWER(table_name) = 'utilisateurs'")
            .ToListAsync();
        if (tables[0] == 0) return;

        var historique = db.GetService<IHistoryRepository>();
        if (await historique.ExistsAsync() && (await historique.GetAppliedMigrationsAsync()).Count > 0) return;

        await historique.CreateIfNotExistsAsync();
        var version = typeof(DbContext).Assembly.GetName().Version!.ToString(3);
        await db.Database.ExecuteSqlRawAsync(historique.GetInsertScript(new HistoryRow(MigrationInitiale, version)));
        journal.LogInformation("Base existante reconnue : migration initiale marquée comme appliquée.");
    }

    private static async Task CreerPremierAdministrateurAsync(ApplicationDbContext db, IConfiguration configuration, ILogger journal)
    {
        if (await db.Comptes.AnyAsync()) return;

        var email = configuration["Admin:Email"];
        if (string.IsNullOrWhiteSpace(email)) email = "admin@universite.local";
        var motDePasse = configuration["Admin:MotDePasse"];
        var genere = string.IsNullOrEmpty(motDePasse);
        if (genere) motDePasse = MotsDePasse.Generer();

        var compte = new Compte
        {
            Email = email.Trim().ToLowerInvariant(),
            Nom = "Administrateur",
            Role = Roles.Administrateur,
            DoitChangerMotDePasse = true
        };
        compte.MotDePasseHash = new PasswordHasher<Compte>().HashPassword(compte, motDePasse!);
        db.Comptes.Add(compte);
        await db.SaveChangesAsync();

        // Affiché une seule fois ; il doit être changé à la première connexion
        if (genere)
            journal.LogWarning("Premier administrateur créé : {Email} / mot de passe provisoire : {MotDePasse}", compte.Email, motDePasse);
        else
            journal.LogWarning("Premier administrateur créé : {Email}", compte.Email);
    }
}

/// <summary>
/// Jeu d'essai fictif, chargé seulement sur une base vide et si Demarrage:DonneesDemo vaut true (jamais chez un
/// client)
/// </summary>
public static class DonneesDemo
{
    /// <summary>
    /// Charge le jeu d'essai : filières, UE, enseignants, étudiants, inscriptions, cours, tarifs, paiement, notes,
    /// salles, séances, absence et bourse. Ne fait rien si la base contient déjà des filières ou des personnes.
    /// </summary>
    /// <param name="db">Contexte de base de données.</param>
    public static async Task ChargerAsync(ApplicationDbContext db)
    {
        if (await db.Filieres.AnyAsync() || await db.Utilisateurs.AnyAsync()) return;

        var niveaux = await db.Niveaux.ToDictionaryAsync(n => n.Code);
        if (!niveaux.TryGetValue("L1", out var l1)) return;

        var info = new Filiere { Code = "INFO", Nom = "Informatique" };
        var gestion = new Filiere { Code = "GES", Nom = "Gestion" };
        var droit = new Filiere { Code = "DRT", Nom = "Droit" };

        var annee = DateTime.Today.Month >= 10 ? DateTime.Today.Year : DateTime.Today.Year - 1;
        var courante = new AnneeAcad { Libelle = $"{annee}-{annee + 1}", DateDebut = new DateTime(annee, 10, 1), DateFin = new DateTime(annee + 1, 7, 31) };

        var algo = new UE { Code = "INF101", Intitule = "Algorithmique", Credits = 6, Filiere = info, Niveau = l1 };
        var bdd = new UE { Code = "INF102", Intitule = "Bases de données", Credits = 5, Filiere = info, Niveau = l1 };
        var compta = new UE { Code = "GES101", Intitule = "Comptabilité générale", Credits = 6, Filiere = gestion, Niveau = l1 };

        var koffi = new Enseignant { Nom = "KOFFI", Prenom = "Jean", Email = "jean.koffi@exemple.test", Grade = "Maître de conférences", Specialite = "Informatique" };
        var mensah = new Enseignant { Nom = "MENSAH", Prenom = "Akossiwa", Email = "akossiwa.mensah@exemple.test", Grade = "Professeur", Specialite = "Gestion" };

        var agbeko = new Etudiant { Nom = "AGBEKO", Prenom = "Kossi", Email = "kossi.agbeko@exemple.test", Matricule = $"ETU{annee}001", DateNaissance = new DateTime(2004, 3, 12) };
        var dossou = new Etudiant { Nom = "DOSSOU", Prenom = "Afi", Email = "afi.dossou@exemple.test", Matricule = $"ETU{annee}002", DateNaissance = new DateTime(2005, 7, 25) };

        var inscription1 = new Inscription { Etudiant = agbeko, Filiere = info, Niveau = l1, AnneeAcad = courante, DateInscription = courante.DateDebut.AddDays(4), Statut = Inscription.Validee };
        var inscription2 = new Inscription { Etudiant = dossou, Filiere = gestion, Niveau = l1, AnneeAcad = courante, DateInscription = courante.DateDebut.AddDays(7), Statut = Inscription.EnAttente };

        var coursAlgo = new Cours { Intitule = "Algorithmique - CM", VolumeHoraire = 30, UE = algo, Enseignant = koffi };
        var coursSql = new Cours { Intitule = "SQL et modélisation", VolumeHoraire = 40, UE = bdd, Enseignant = koffi };
        var amphi = new Salle { Code = "AMPHI-A", Nom = "Amphithéâtre A", Type = "Amphithéâtre", Capacite = 300 };
        var labo = new Salle { Code = "B12", Nom = "Salle informatique B12", Type = "Salle informatique", Capacite = 30 };

        // Emploi du temps de la semaine en cours, ou de la deuxième semaine de l'année hors période de cours
        static DateTime Lundi(DateTime jour) => jour.AddDays(-(((int)jour.DayOfWeek + 6) % 7));
        var lundi = Lundi(DateTime.Today);
        if (lundi < courante.DateDebut || lundi.AddDays(5) > courante.DateFin) lundi = Lundi(courante.DateDebut).AddDays(7);
        var seanceAlgo = new Seance { Cours = coursAlgo, Salle = amphi, Date = lundi, HeureDebut = new(8, 0, 0), HeureFin = new(10, 0, 0) };

        db.AddRange(droit, algo, bdd, compta, koffi, mensah, inscription1, inscription2, coursAlgo, coursSql, amphi, labo, seanceAlgo);
        db.AddRange(
            new Cours { Intitule = "Comptabilité - TD", VolumeHoraire = 20, UE = compta, Enseignant = mensah },
            new Seance { Cours = coursSql, Salle = labo, Type = "Travaux pratiques", Date = lundi.AddDays(2), HeureDebut = new(10, 0, 0), HeureFin = new(12, 30, 0) },
            new Seance { Cours = coursAlgo, Salle = amphi, Type = Seance.Examen, Date = lundi.AddDays(4), HeureDebut = new(14, 0, 0), HeureFin = new(16, 0, 0) },
            new Absence { Seance = seanceAlgo, Inscription = inscription1, Justifiee = true, Motif = "Certificat médical" },
            new Aide { Inscription = inscription2, Type = "Bourse sociale", Montant = 100000m, Motif = "Commission des bourses" },
            new Tarif { AnneeAcad = courante, Filiere = info, Niveau = l1, Montant = 350000m },
            new Tarif { AnneeAcad = courante, Filiere = gestion, Niveau = l1, Montant = 300000m },
            new Paiement { Inscription = inscription1, Montant = 150000m, DatePaiement = inscription1.DateInscription, Mode = "Mobile Money" },
            new Note { Inscription = inscription1, UE = algo, Valeur = 13.5m },
            new Note { Inscription = inscription1, UE = bdd, Valeur = 8m, Rattrapage = 11m });
        await db.SaveChangesAsync();
    }
}
