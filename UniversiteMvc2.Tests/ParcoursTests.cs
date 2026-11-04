using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;
using Xunit;

namespace UniversiteMvc2.Tests;

// Ces tests exigent un serveur MySQL/MariaDB : définir TEST_MYSQL avec une chaîne de connexion
// dont le compte peut créer des bases, par exemple
//     Server=127.0.0.1;Port=3306;User=root;Password=;
// Une base temporaire est créée puis supprimée. Sans TEST_MYSQL, ils sont signalés « ignorés ».
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST_MYSQL")))
            Skip = "TEST_MYSQL non défini";
    }
}

public sealed class Application : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminProvisoire = "provisoire-Test-123";

    private readonly string _base = "test_" + Guid.NewGuid().ToString("N");
    private readonly string _serveur = Environment.GetEnvironmentVariable("TEST_MYSQL") ?? "";

    public int EssaisParMinute { get; init; } = 1000;

    // Tout ce que l'application écrit dans ses logs pendant le test
    public ConcurrentQueue<string> Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logs => logs.AddProvider(new CapteurLogs(Logs)));
        builder.ConfigureTestServices(services => services.AddControllersWithViews().AddApplicationPart(typeof(Application).Assembly));

        using (var connexion = new MySqlConnection(_serveur))
        {
            connexion.Open();
            using var commande = new MySqlCommand($"CREATE DATABASE `{_base}` CHARACTER SET utf8mb4", connexion);
            commande.ExecuteNonQuery();
        }

        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = $"{_serveur.TrimEnd(';')};Database={_base};",
            ["Admin:Email"] = AdminEmail,
            ["Admin:MotDePasse"] = AdminProvisoire,
            ["Demarrage:DonneesDemo"] = "true",
            ["Connexion:EssaisParMinute"] = EssaisParMinute.ToString(),
            ["Etablissement:Nom"] = "Institut de Test"
        }));
    }

    public HttpClient Navigateur() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });

    public async Task<T> AvecBaseAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        using var connexion = new MySqlConnection(_serveur);
        connexion.Open();
        using var commande = new MySqlCommand($"DROP DATABASE IF EXISTS `{_base}`", connexion);
        commande.ExecuteNonQuery();
    }
}

public sealed class CapteurLogs(ConcurrentQueue<string> logs) : ILoggerProvider, ILogger
{
    public ILogger CreateLogger(string categorie) => this;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel niveau) => niveau >= LogLevel.Information;
    public void Dispose() { }

    public void Log<TState>(LogLevel niveau, EventId id, TState state, Exception? erreur, Func<TState, Exception?, string> format) =>
        logs.Enqueue($"{niveau}: {format(state, erreur)} {erreur?.Message}");
}

// Simule une panne imprévue : seuls les tests connaissent cette page
[AllowAnonymous]
public class PanneController : Controller
{
    public IActionResult Index() => throw new InvalidOperationException("detail-interne-secret");
}

public static class Pages
{
    // Le texte tel qu'affiché : Razor encode notamment les apostrophes
    public static async Task<string> TexteAsync(this HttpResponseMessage reponse) =>
        System.Net.WebUtility.HtmlDecode(await reponse.Content.ReadAsStringAsync());

    public static async Task<string> JetonAsync(this HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        return Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> EnvoyerAsync(this HttpClient client, string urlFormulaire, string urlCible, params (string Cle, string Valeur)[] champs)
    {
        var jeton = await client.JetonAsync(urlFormulaire);
        var valeurs = champs.Select(c => new KeyValuePair<string, string>(c.Cle, c.Valeur)).ToList();
        valeurs.Add(new("__RequestVerificationToken", jeton));
        return await client.PostAsync(urlCible, new FormUrlEncodedContent(valeurs));
    }

    public static Task<HttpResponseMessage> ConnecterAsync(this HttpClient client, string email, string motDePasse) =>
        client.EnvoyerAsync("/Connexion", "/Connexion", ("email", email), ("motDePasse", motDePasse));

    // Connexion administrateur, y compris le changement obligatoire du mot de passe au premier passage
    public static async Task<HttpClient> AdminAsync(this Application app)
    {
        var client = app.Navigateur();
        var reponse = await client.ConnecterAsync(Application.AdminEmail, Application.AdminProvisoire);
        if (reponse.Headers.Location?.ToString().Contains("MotDePasse") == true)
        {
            await client.EnvoyerAsync("/Connexion/MotDePasse", "/Connexion/MotDePasse",
                ("actuel", Application.AdminProvisoire), ("nouveau", "definitif-Test-456"), ("confirmation", "definitif-Test-456"));
        }
        else
        {
            await client.ConnecterAsync(Application.AdminEmail, "definitif-Test-456");
        }
        return client;
    }

    // Crée un compte par l'interface d'administration, et renvoie un navigateur connecté avec ce compte
    public static async Task<HttpClient> ProfilAsync(this Application app, HttpClient admin, string role)
    {
        var email = $"{Guid.NewGuid():N}@test.local";
        var creation = await admin.EnvoyerAsync("/Comptes/Create", "/Comptes/Create", ("Email", email), ("Nom", role), ("Role", role));
        Assert.Equal(HttpStatusCode.Redirect, creation.StatusCode);

        var page = await admin.GetStringAsync("/Comptes");
        var provisoire = Regex.Match(page, Regex.Escape(email) + " : ([A-Za-z0-9]+)").Groups[1].Value;
        Assert.NotEmpty(provisoire);

        var client = app.Navigateur();
        await client.ConnecterAsync(email, provisoire);
        await client.EnvoyerAsync("/Connexion/MotDePasse", "/Connexion/MotDePasse",
            ("actuel", provisoire), ("nouveau", "definitif-Test-456"), ("confirmation", "definitif-Test-456"));
        return client;
    }
}

public class ParcoursTests
{
    [MySqlFact]
    public async Task Sans_connexion_tout_redirige_sauf_les_pages_publiques()
    {
        using var app = new Application();
        var client = app.Navigateur();

        foreach (var url in new[] { "/", "/Etudiants", "/Paiements", "/Comptes", "/Journal", "/Documents" })
        {
            var reponse = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, reponse.StatusCode);
            Assert.Contains("/Connexion", reponse.Headers.Location!.ToString());
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Connexion")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/sante")).StatusCode);
    }

    [MySqlFact]
    public async Task Le_premier_mot_de_passe_doit_etre_change_avant_tout_acces()
    {
        using var app = new Application();
        var client = app.Navigateur();

        var connexion = await client.ConnecterAsync(Application.AdminEmail, Application.AdminProvisoire);
        Assert.Contains("MotDePasse", connexion.Headers.Location!.ToString());

        var bloque = await client.GetAsync("/Etudiants");
        Assert.Contains("MotDePasse", bloque.Headers.Location!.ToString());

        // Un mot de passe trop faible est refusé
        var faible = await client.EnvoyerAsync("/Connexion/MotDePasse", "/Connexion/MotDePasse",
            ("actuel", Application.AdminProvisoire), ("nouveau", "court"), ("confirmation", "court"));
        Assert.Equal(HttpStatusCode.OK, faible.StatusCode);

        var change = await client.EnvoyerAsync("/Connexion/MotDePasse", "/Connexion/MotDePasse",
            ("actuel", Application.AdminProvisoire), ("nouveau", "definitif-Test-456"), ("confirmation", "definitif-Test-456"));
        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Etudiants")).StatusCode);
    }

    [MySqlFact]
    public async Task Cinq_echecs_verrouillent_le_compte_meme_avec_le_bon_mot_de_passe()
    {
        using var app = new Application();
        var client = app.Navigateur();

        for (var i = 0; i < 5; i++)
            await client.ConnecterAsync(Application.AdminEmail, "mauvais-mot-de-passe-1");

        var reponse = await client.ConnecterAsync(Application.AdminEmail, Application.AdminProvisoire);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Contains("verrouillé", await reponse.Content.ReadAsStringAsync());
    }

    [MySqlFact]
    public async Task Le_nombre_d_essais_par_minute_est_limite()
    {
        using var app = new Application { EssaisParMinute = 3 };
        var client = app.Navigateur();
        var jeton = await client.JetonAsync("/Connexion");

        HttpResponseMessage? derniere = null;
        for (var i = 0; i < 4; i++)
        {
            derniere = await client.PostAsync("/Connexion", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = "x@test.local", ["motDePasse"] = "x", ["__RequestVerificationToken"] = jeton
            }));
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, derniere!.StatusCode);
    }

    [MySqlFact]
    public async Task Une_redirection_externe_apres_connexion_est_refusee()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();
        await admin.PostAsync("/Connexion/Deconnexion", new FormUrlEncodedContent([]));

        var client = app.Navigateur();
        var reponse = await client.EnvoyerAsync("/Connexion", "/Connexion?retour=https://malveillant.example",
            ("email", Application.AdminEmail), ("motDePasse", "definitif-Test-456"));
        Assert.Equal("/", reponse.Headers.Location!.ToString());
    }

    [MySqlFact]
    public async Task Les_profils_sont_appliques_cote_serveur_et_dans_les_pages()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();
        var lecteur = await app.ProfilAsync(admin, Roles.Consultation);
        var scolarite = await app.ProfilAsync(admin, Roles.Scolarite);

        // Consultation : lit, sans lien d'écriture ; pas de création ni d'export ; pas d'administration
        var liste = await lecteur.GetAsync("/Etudiants");
        Assert.Equal(HttpStatusCode.OK, liste.StatusCode);
        var html = await liste.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Nouvel étudiant", html);
        Assert.DoesNotContain("Exporter", html);
        Assert.DoesNotContain("/Comptes", html);

        Assert.Equal(HttpStatusCode.Forbidden, (await lecteur.GetAsync("/Etudiants/Create")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lecteur.GetAsync("/Etudiants/Exporter")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lecteur.GetAsync("/Comptes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lecteur.GetAsync("/Journal")).StatusCode);

        // Un POST forgé est refusé, même avec un jeton anti-CSRF valide
        var jeton = await lecteur.JetonAsync("/Documents");
        var forge = await lecteur.PostAsync("/Etudiants/Delete/1", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = jeton }));
        Assert.Equal(HttpStatusCode.Forbidden, forge.StatusCode);

        // Scolarité : écrit sur les étudiants, pas sur les paiements
        Assert.Equal(HttpStatusCode.OK, (await scolarite.GetAsync("/Etudiants/Create")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scolarite.GetAsync("/Paiements/Create")).StatusCode);
    }

    [MySqlFact]
    public async Task Desactiver_un_compte_coupe_sa_session_immediatement()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();
        var lecteur = await app.ProfilAsync(admin, Roles.Consultation);
        Assert.Equal(HttpStatusCode.OK, (await lecteur.GetAsync("/Etudiants")).StatusCode);

        var id = await app.AvecBaseAsync(db => db.Comptes.Where(c => c.Role == Roles.Consultation).Select(c => c.Id).FirstAsync());
        var reponse = await admin.EnvoyerAsync($"/Comptes/Edit/{id}", $"/Comptes/Edit/{id}", ("Nom", "Désactivé"), ("Role", Roles.Consultation), ("Actif", "false"));
        Assert.Equal(HttpStatusCode.Redirect, reponse.StatusCode);

        Assert.Equal(HttpStatusCode.Redirect, (await lecteur.GetAsync("/Etudiants")).StatusCode);
    }

    [MySqlFact]
    public async Task Le_dernier_administrateur_ne_peut_pas_etre_retire()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();
        var id = await app.AvecBaseAsync(db => db.Comptes.Select(c => c.Id).FirstAsync());

        var reponse = await admin.EnvoyerAsync($"/Comptes/Edit/{id}", $"/Comptes/Edit/{id}", ("Nom", "Admin"), ("Role", Roles.Consultation), ("Actif", "true"));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Contains("au moins un administrateur", await reponse.Content.ReadAsStringAsync());
        Assert.Equal(Roles.Administrateur, await app.AvecBaseAsync(db => db.Comptes.Select(c => c.Role).FirstAsync()));
    }

    [MySqlFact]
    public async Task Toutes_les_pages_s_affichent()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();

        var (inscription, paiement, tarif) = await app.AvecBaseAsync(async db => (
            await db.Inscriptions.Where(i => i.Statut == Inscription.Validee).Select(i => i.Id).FirstAsync(),
            await db.Paiements.Select(p => p.Id).FirstAsync(),
            await db.Tarifs.Select(t => t.Id).FirstAsync()));
        var ids = await app.AvecBaseAsync(async db => new Dictionary<string, int>
        {
            ["Etudiants"] = await db.Etudiants.Select(e => e.Id).FirstAsync(),
            ["Enseignants"] = await db.Enseignants.Select(e => e.Id).FirstAsync(),
            ["Filieres"] = await db.Filieres.Select(e => e.Id).FirstAsync(),
            ["Niveaux"] = await db.Niveaux.Select(e => e.Id).FirstAsync(),
            ["UEs"] = await db.UEs.Select(e => e.Id).FirstAsync(),
            ["Cours"] = await db.Cours.Select(e => e.Id).FirstAsync(),
            ["AnneesAcad"] = await db.AnneesAcad.Select(e => e.Id).FirstAsync(),
            ["Inscriptions"] = inscription,
            ["Paiements"] = paiement,
            ["Tarifs"] = tarif,
            ["Salles"] = await db.Salles.Select(e => e.Id).FirstAsync(),
            ["Seances"] = await db.Seances.Select(e => e.Id).FirstAsync(),
            ["Aides"] = await db.Aides.Select(e => e.Id).FirstAsync()
        });

        var urls = new List<string> { "/", "/Notes", "/Documents", "/Comptes", "/Comptes/Create", "/Journal", "/Paiements/Impayes", "/Connexion/MotDePasse",
            "/Absences", $"/Absences/Appel?seanceId={ids["Seances"]}", "/Deliberations", $"/Aides/Create?inscriptionId={inscription}" };
        foreach (var (controleur, id) in ids)
            urls.AddRange([$"/{controleur}", $"/{controleur}/Create", $"/{controleur}/Details/{id}", $"/{controleur}/Edit/{id}", $"/{controleur}/Delete/{id}"]);

        foreach (var url in urls)
        {
            var reponse = await admin.GetAsync(url);
            Assert.True(reponse.StatusCode == HttpStatusCode.OK, $"{url} → {(int)reponse.StatusCode}");
            var html = await reponse.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Toronto", html);
            Assert.Contains("Institut de Test", html);
        }
    }

    [MySqlFact]
    public async Task Un_paiement_ne_peut_pas_depasser_le_reste_a_payer()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();
        // Démonstration : tarif de 350 000, déjà 150 000 versés, soit 200 000 de reste
        var id = await app.AvecBaseAsync(db => db.Paiements.Select(p => p.InscriptionId).FirstAsync());
        var date = DateTime.Today.ToString("yyyy-MM-dd");

        var trop = await admin.EnvoyerAsync("/Paiements/Create", "/Paiements/Create",
            ("InscriptionId", id.ToString()), ("Montant", "200001"), ("DatePaiement", date), ("Mode", "Espèces"));
        Assert.Equal(HttpStatusCode.OK, trop.StatusCode);
        Assert.Contains("dépasse le reste", await trop.Content.ReadAsStringAsync());

        var juste = await admin.EnvoyerAsync("/Paiements/Create", "/Paiements/Create",
            ("InscriptionId", id.ToString()), ("Montant", "200000"), ("DatePaiement", date), ("Mode", "Espèces"));
        Assert.Equal(HttpStatusCode.Redirect, juste.StatusCode);

        var impayes = await admin.GetStringAsync("/Paiements/Impayes");
        Assert.Contains("Aucun impayé", impayes);

        Assert.Equal(350000m, await app.AvecBaseAsync(db => db.Paiements.Where(p => p.InscriptionId == id).SumAsync(p => p.Montant)));
    }

    [MySqlFact]
    public async Task Les_notes_sont_saisies_controlees_et_imprimees()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();
        var (inscription, annee, ue) = await app.AvecBaseAsync(async db =>
        {
            var i = await db.Inscriptions.FirstAsync(x => x.Statut == Inscription.Validee);
            var u = await db.UEs.FirstAsync(x => x.Code == "INF101");
            return (i.Id, i.AnneeAcadId, u.Id);
        });
        var grille = $"/Notes?anneeId={annee}&ueId={ue}";

        var horsBareme = await admin.EnvoyerAsync(grille, grille,
            ("lignes[0].InscriptionId", inscription.ToString()), ("lignes[0].Valeur", "25"), ("lignes[0].Rattrapage", ""));
        Assert.Equal(HttpStatusCode.OK, horsBareme.StatusCode);
        Assert.Contains("Rien n'a été enregistré", await horsBareme.TexteAsync());
        Assert.Equal(13.5m, await app.AvecBaseAsync(db => db.Notes.Where(n => n.InscriptionId == inscription && n.UEId == ue).Select(n => n.Valeur).FirstAsync()));

        var correct = await admin.EnvoyerAsync(grille, grille,
            ("lignes[0].InscriptionId", inscription.ToString()), ("lignes[0].Valeur", "9.5"), ("lignes[0].Rattrapage", "12.25"));
        Assert.Equal(HttpStatusCode.Redirect, correct.StatusCode);
        var note = await app.AvecBaseAsync(db => db.Notes.AsNoTracking().FirstAsync(n => n.InscriptionId == inscription && n.UEId == ue));
        Assert.Equal(9.5m, note.Valeur);
        Assert.Equal(12.25m, note.Rattrapage);

        var pdf = await admin.GetAsync($"/Documents/Telecharger?type={TypeDocument.ReleveNotes}&id={inscription}");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync())[..4]);

        var fiche = await admin.GetStringAsync($"/Inscriptions/Details/{inscription}");
        Assert.Contains("Relevé de notes PDF", fiche);
    }

    [MySqlFact]
    public async Task L_emploi_du_temps_refuse_les_conflits_et_l_appel_alimente_l_assiduite()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();
        // Démonstration : « Algorithmique - CM » le lundi de la semaine, de 8 h à 10 h, en AMPHI-A, par KOFFI
        var (seance, coursSql, inscription, annee) = await app.AvecBaseAsync(async db => (
            await db.Seances.AsNoTracking().FirstAsync(s => s.Cours!.Intitule == "Algorithmique - CM" && s.Type != Seance.Examen),
            await db.Cours.Where(c => c.Intitule == "SQL et modélisation").Select(c => c.Id).FirstAsync(),
            await db.Inscriptions.Where(i => i.Statut == Inscription.Validee).Select(i => i.Id).FirstAsync(),
            await db.AnneesAcad.Select(a => a.Id).FirstAsync()));
        var date = seance.Date.ToString("yyyy-MM-dd");

        // Même salle, même enseignant, même promotion, horaires qui se croisent
        var conflit = await admin.EnvoyerAsync("/Seances/Create", "/Seances/Create",
            ("CoursId", coursSql.ToString()), ("Type", "Travaux dirigés"), ("Date", date), ("HeureDebut", "09:00"), ("HeureFin", "11:00"),
            ("SalleId", seance.SalleId.ToString()!));
        Assert.Equal(HttpStatusCode.OK, conflit.StatusCode);
        var texte = await conflit.TexteAsync();
        Assert.Contains("déjà occupée", texte);
        Assert.Contains("assure déjà", texte);

        var libre = await admin.EnvoyerAsync("/Seances/Create", "/Seances/Create",
            ("CoursId", coursSql.ToString()), ("Type", "Travaux dirigés"), ("Date", date), ("HeureDebut", "10:00"), ("HeureFin", "11:30"), ("SalleId", ""));
        Assert.Equal(HttpStatusCode.Redirect, libre.StatusCode);

        // L'absence justifiée de la démonstration devient non justifiée
        var appel = $"/Absences/Appel?seanceId={seance.Id}";
        var enregistre = await admin.EnvoyerAsync(appel, appel,
            ("lignes[0].InscriptionId", inscription.ToString()), ("lignes[0].Absent", "true"), ("lignes[0].Motif", ""));
        Assert.Equal(HttpStatusCode.Redirect, enregistre.StatusCode);
        var absence = await app.AvecBaseAsync(db => db.Absences.AsNoTracking().SingleAsync(a => a.SeanceId == seance.Id));
        Assert.False(absence.Justifiee);

        Assert.Contains("dont 2 h non justifiées", await admin.GetStringAsync($"/Absences?anneeId={annee}"));
        Assert.Contains("AGBEKO", await admin.GetStringAsync($"/Absences/Exporter?anneeId={annee}"));
    }

    [MySqlFact]
    public async Task Une_bourse_reduit_le_reste_a_payer_sans_depasser_le_tarif()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();
        // Démonstration : tarif de 350 000, déjà 150 000 versés
        var id = await app.AvecBaseAsync(db => db.Paiements.Select(p => p.InscriptionId).FirstAsync());

        var excessive = await admin.EnvoyerAsync("/Aides/Create", "/Aides/Create",
            ("InscriptionId", id.ToString()), ("Type", "Exonération"), ("Montant", "350001"));
        Assert.Equal(HttpStatusCode.OK, excessive.StatusCode);
        Assert.Contains("dépasseraient le tarif", await excessive.TexteAsync());

        var bourse = await admin.EnvoyerAsync("/Aides/Create", "/Aides/Create",
            ("InscriptionId", id.ToString()), ("Type", "Bourse sociale"), ("Montant", "100000"), ("Motif", "Commission"));
        Assert.Equal(HttpStatusCode.Redirect, bourse.StatusCode);

        // Reste : 350 000 − 100 000 − 150 000 = 100 000
        var trop = await admin.EnvoyerAsync("/Paiements/Create", "/Paiements/Create",
            ("InscriptionId", id.ToString()), ("Montant", "100001"), ("DatePaiement", DateTime.Today.ToString("yyyy-MM-dd")), ("Mode", "Espèces"));
        Assert.Contains("dépasse le reste", await trop.TexteAsync());
        Assert.Contains("Bourses et exonérations", await admin.GetStringAsync($"/Inscriptions/Details/{id}"));
    }

    [MySqlFact]
    public async Task Les_deliberations_donnent_decisions_export_et_proces_verbal()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();
        var inscription = await app.AvecBaseAsync(db => db.Inscriptions.AsNoTracking().FirstAsync(i => i.Statut == Inscription.Validee));

        // Démonstration : 13,5 en INF101 et 11 (rattrapage) en INF102 : toutes les UE validées
        var page = await admin.GetStringAsync($"/Deliberations?anneeId={inscription.AnneeAcadId}&filiereId={inscription.FiliereId}&niveauId={inscription.NiveauId}");
        Assert.Contains("AGBEKO", page);
        Assert.Contains("<strong>Admis</strong>", page);

        Assert.Contains("AGBEKO", await admin.GetStringAsync($"/Deliberations/Exporter?anneeId={inscription.AnneeAcadId}"));

        var pdf = await admin.EnvoyerAsync("/Documents", "/Documents",
            ("Type", TypeDocument.ProcesVerbal.ToString()), ("AnneeAcadId", inscription.AnneeAcadId.ToString()),
            ("FiliereId", inscription.FiliereId.ToString()), ("NiveauId", inscription.NiveauId.ToString()),
            ("Signataire", "Le président du jury"), ("Lieu", "Lomé"), ("DateDocument", DateTime.Today.ToString("yyyy-MM-dd")));
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
    }

    [MySqlFact]
    public async Task Tous_les_documents_PDF_sont_generes()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();
        var (inscription, paiement) = await app.AvecBaseAsync(async db =>
            (await db.Inscriptions.Where(i => i.Statut == Inscription.Validee).Select(i => i.Id).FirstAsync(), await db.Paiements.Select(p => p.Id).FirstAsync()));

        foreach (var url in new[]
        {
            $"/Documents/Telecharger?type={TypeDocument.AttestationInscription}&id={inscription}",
            $"/Documents/Telecharger?type={TypeDocument.RelevePaiements}&id={inscription}",
            $"/Documents/Telecharger?type={TypeDocument.RecuPaiement}&id={paiement}"
        })
        {
            var pdf = await admin.GetAsync(url);
            Assert.True(pdf.Content.Headers.ContentType?.MediaType == "application/pdf", url);
        }
    }

    [MySqlFact]
    public async Task Les_exports_CSV_fonctionnent()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();

        foreach (var url in new[] { "/Etudiants/Exporter", "/Inscriptions/Exporter", "/Paiements/Exporter", "/Paiements/ExporterImpayes" })
        {
            var reponse = await admin.GetAsync(url);
            Assert.True(reponse.StatusCode == HttpStatusCode.OK, url);
            Assert.Equal("text/csv", reponse.Content.Headers.ContentType!.MediaType);
        }
        Assert.Contains("AGBEKO", await admin.GetStringAsync("/Etudiants/Exporter"));
    }

    [MySqlFact]
    public async Task Les_ecritures_sont_journalisees_avec_leur_auteur_sans_mot_de_passe()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();

        var creation = await admin.EnvoyerAsync("/Filieres/Create", "/Filieres/Create", ("Code", "MED"), ("Nom", "Médecine"));
        Assert.Equal(HttpStatusCode.Redirect, creation.StatusCode);

        var lignes = await app.AvecBaseAsync(db => db.JournalAudit.AsNoTracking().ToListAsync());
        var trace = Assert.Single(lignes, j => j.Entite == nameof(Filiere) && j.Action == "Création" && j.Details!.Contains("MED"));
        Assert.Equal(Application.AdminEmail, trace.Auteur);
        Assert.NotNull(trace.EntiteId);

        Assert.Contains(lignes, j => j.Action == "Connexion");
        Assert.Contains(lignes, j => j.Entite == nameof(Compte) && j.Details?.Contains("Mot de passe modifié") == true);
        Assert.DoesNotContain(lignes, j => j.Details?.Contains("AQAAAA") == true);
    }

    [MySqlFact]
    public async Task Les_doublons_sont_refuses_sans_erreur_serveur()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();

        var doublon = await admin.EnvoyerAsync("/UEs/Create", "/UEs/Create",
            ("Code", "INF101"), ("Intitule", "Doublon"), ("Credits", "3"), ("FiliereId", "1"), ("NiveauId", "1"));
        Assert.Equal(HttpStatusCode.OK, doublon.StatusCode);
        Assert.Contains("existe déjà", await doublon.Content.ReadAsStringAsync());
    }

    [MySqlFact]
    public async Task Un_statut_inconnu_est_refuse()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();
        var i = await app.AvecBaseAsync(db => db.Inscriptions.AsNoTracking().FirstAsync());

        var reponse = await admin.EnvoyerAsync($"/Inscriptions/Edit/{i.Id}", $"/Inscriptions/Edit/{i.Id}",
            ("DateInscription", "2026-01-01"), ("Statut", "Pirate"), ("EtudiantId", i.EtudiantId.ToString()),
            ("FiliereId", i.FiliereId.ToString()), ("NiveauId", i.NiveauId.ToString()), ("AnneeAcadId", i.AnneeAcadId.ToString()));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(i.Statut, await app.AvecBaseAsync(db => db.Inscriptions.Where(x => x.Id == i.Id).Select(x => x.Statut).FirstAsync()));
    }

    [MySqlFact]
    public async Task Les_en_tetes_de_securite_sont_presents()
    {
        using var app = new Application();
        var reponse = await app.Navigateur().GetAsync("/Connexion");

        Assert.Equal("nosniff", reponse.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", reponse.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("script-src 'self'", reponse.Headers.GetValues("Content-Security-Policy").Single());
    }

    [MySqlFact]
    public async Task Une_panne_affiche_une_page_generique_et_le_detail_part_dans_les_logs()
    {
        using var app = new Application();
        var reponse = await app.Navigateur().GetAsync("/Panne");

        Assert.Equal(HttpStatusCode.InternalServerError, reponse.StatusCode);
        var page = await reponse.TexteAsync();
        Assert.Contains("Une erreur est survenue", page);
        Assert.DoesNotContain("detail-interne-secret", page);
        Assert.DoesNotContain("PanneController", page);
        Assert.Contains(app.Logs, l => l.StartsWith("Error") && l.Contains("detail-interne-secret"));
    }

    [MySqlFact]
    public async Task Un_formulaire_sans_jeton_anti_CSRF_est_refuse()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();

        var reponse = await admin.PostAsync("/Filieres/Create", new FormUrlEncodedContent(new Dictionary<string, string> { ["Code"] = "CSRF", ["Nom"] = "Forgé" }));

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("Demande invalide", await reponse.TexteAsync());
        Assert.False(await app.AvecBaseAsync(db => db.Filieres.AnyAsync(f => f.Code == "CSRF")));
    }

    [MySqlFact]
    public async Task Les_evenements_de_securite_partent_dans_les_logs_sans_mot_de_passe()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();
        var lecteur = await app.ProfilAsync(admin, Roles.Consultation);
        await lecteur.GetAsync("/Comptes");

        // Une saisie avec un retour à la ligne ne doit pas pouvoir fabriquer une fausse ligne de log
        await app.Navigateur().ConnecterAsync("pirate@test.local\nFAUX", "mot-de-passe-Essai-1");

        Assert.Contains(app.Logs, l => l.StartsWith("Warning") && l.Contains("Accès refusé") && l.Contains("Comptes/Index"));
        Assert.Contains(app.Logs, l => l.StartsWith("Warning") && l.Contains("Connexion refusée") && l.Contains("pirate@test.localfaux"));
        Assert.Contains(app.Logs, l => l.Contains($"Connexion de {Application.AdminEmail}"));
        Assert.DoesNotContain(app.Logs, l => l.Contains("mot-de-passe-Essai-1") || l.Contains("definitif-Test-456"));
    }

    [MySqlFact]
    public async Task Le_verrouillage_et_la_deconnexion_sont_journalises()
    {
        using var app = new Application();
        var client = app.Navigateur();
        for (var i = 0; i < 5; i++)
            await client.ConnecterAsync(Application.AdminEmail, "mauvais-mot-de-passe-1");

        var admin = await app.AvecBaseAsync(db => db.JournalAudit.AsNoTracking().Where(j => j.Action == "Connexion refusée").ToListAsync());
        Assert.Equal(5, admin.Count);
        Assert.Single(admin, j => j.Details?.Contains("verrouillé") == true);
        Assert.Contains(app.Logs, l => l.Contains("Compte verrouillé"));

        await app.AvecBaseAsync(async db =>
        {
            await db.Comptes.ExecuteUpdateAsync(c => c.SetProperty(x => x.VerrouilleJusquA, (DateTime?)null));
            return true;
        });
        var session = await app.AdminAsync();
        var deconnexion = await session.EnvoyerAsync("/Documents", "/Connexion/Deconnexion");
        Assert.Equal(HttpStatusCode.Redirect, deconnexion.StatusCode);
        Assert.True(await app.AvecBaseAsync(db => db.JournalAudit.AnyAsync(j => j.Action == "Déconnexion" && j.Auteur == Application.AdminEmail)));
    }

    [MySqlFact]
    public async Task Un_administrateur_recupere_son_compte_en_ligne_de_commande()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();

        Assert.Null(await Demarrage.ReinitialiserMotDePasseAsync(app.Services, "inconnu@test.local"));
        var provisoire = await Demarrage.ReinitialiserMotDePasseAsync(app.Services, Application.AdminEmail.ToUpperInvariant());
        Assert.NotNull(provisoire);

        // L'ancienne session et l'ancien mot de passe ne valent plus rien ; le provisoire doit être changé
        Assert.Equal(HttpStatusCode.Redirect, (await admin.GetAsync("/Etudiants")).StatusCode);
        var client = app.Navigateur();
        Assert.Equal(HttpStatusCode.OK, (await client.ConnecterAsync(Application.AdminEmail, "definitif-Test-456")).StatusCode);
        var connexion = await client.ConnecterAsync(Application.AdminEmail, provisoire!);
        Assert.Contains("MotDePasse", connexion.Headers.Location!.ToString());
    }

    [MySqlFact]
    public async Task Un_type_de_document_inconnu_renvoie_404()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/Documents/Telecharger?type=99&id=1")).StatusCode);
    }

    [MySqlFact]
    public async Task Une_page_inexistante_renvoie_404_et_un_message_lisible()
    {
        using var app = new Application();
        var admin = await app.AdminAsync();

        var reponse = await admin.GetAsync("/Etudiants/Details/999999");
        Assert.Equal(HttpStatusCode.NotFound, reponse.StatusCode);
        Assert.Contains("Cette page n'existe pas", await reponse.TexteAsync());
    }
}
