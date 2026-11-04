using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.WebEncoders;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using UniversiteMvc2.Data;
using UniversiteMvc2.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    MessagesValidationFr.Configurer(options);
    options.Filters.Add<DroitsFilter>();
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Accents et caractères français écrits tels quels dans le HTML (au lieu de &#xE9;…)
builder.Services.Configure<WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// Vues générées : si un contrôleur n'a pas sa propre vue (Index, Details, Formulaire),
// Razor prend celle de Views/Shared/Generique, construite à partir du modèle
builder.Services.Configure<RazorViewEngineOptions>(options =>
    options.ViewLocationFormats.Add("/Views/Shared/Generique/{0}.cshtml"));
builder.Services.AddScoped<Modeles>();

// Documents PDF : identité de l'établissement + licence QuestPDF Community
// (gratuite sous conditions de chiffre d'affaires : https://www.questpdf.com/license/)
builder.Services.Configure<Etablissement>(builder.Configuration.GetSection("Etablissement"));
QuestPDF.Settings.License = LicenseType.Community;

// Injection de dépendances : enregistrement du DbContext (MySQL/MariaDB du conteneur Docker, géré via phpMyAdmin)
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<ApplicationDbContext>((services, options) =>
    options.UseMySQL(services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!)
        .ReplaceService<Microsoft.EntityFrameworkCore.Migrations.IHistoryRepository, HistoriqueMigrations>());

// Connexion par cookie : toute page exige un utilisateur connecté, sauf celles marquées [AllowAnonymous]
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Connexion";
        options.ReturnUrlParameter = "retour";
        options.Cookie.Name = "universite.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToAccessDenied = contexte =>
        {
            contexte.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        // Compte désactivé, mot de passe ou profil changé : la session s'arrête à la requête suivante
        options.Events.OnValidatePrincipal = async contexte =>
        {
            var db = contexte.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var id = int.TryParse(contexte.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var n) ? n : 0;
            var jeton = contexte.Principal?.FindFirst(Droits.ClaimJeton)?.Value;
            if (!await db.Comptes.AnyAsync(c => c.Id == id && c.Actif && c.Jeton == jeton))
            {
                contexte.RejectPrincipal();
                await contexte.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Limite les essais de mot de passe par adresse IP, en plus du verrouillage par compte
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("connexion", contexte => RateLimitPartition.GetFixedWindowLimiter(
        contexte.Connection.RemoteIpAddress?.ToString() ?? "inconnue",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = contexte.RequestServices.GetRequiredService<IConfiguration>().GetValue("Connexion:EssaisParMinute", 10), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Sans clés persistantes, chaque redémarrage déconnecterait tout le monde et invaliderait les formulaires ouverts
var dossierCles = builder.Configuration["DataProtection:Dossier"];
var protection = builder.Services.AddDataProtection().SetApplicationName("UniversiteMvc2");
if (!string.IsNullOrEmpty(dossierCles))
    protection.PersistKeysToFileSystem(new DirectoryInfo(dossierCles));

// Derrière un reverse proxy (Caddy, Nginx) : schéma HTTPS et adresse IP réelle du client.
// À n'activer que si l'application n'est joignable qu'à travers le proxy.
var derriereProxy = builder.Configuration.GetValue<bool>("ReverseProxy:Actif");
if (derriereProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddHealthChecks().AddCheck<BaseDeDonneesCheck>("base");

var app = builder.Build();

if (derriereProxy) app.UseForwardedHeaders();

// Mot de passe administrateur perdu : dotnet UniversiteMvc2.dll reinitialiser-mot-de-passe <e-mail>
// Affiché sur la console uniquement, pour qu'il n'entre pas dans les logs. Le serveur web ne démarre pas.
if (args is [Demarrage.CommandeReinitialisation, var email])
{
    var provisoire = await Demarrage.ReinitialiserMotDePasseAsync(app.Services, email);
    Console.WriteLine(provisoire == null ? $"Aucun compte pour {email}." : $"Mot de passe provisoire de {email} : {provisoire}");
    Environment.ExitCode = provisoire == null ? 1 : 0;
    return;
}

// Schéma de la base (migrations), référentiel minimal et premier administrateur au démarrage
await Demarrage.PreparerAsync(app.Services);

// En-têtes de sécurité sur toutes les réponses.
// style-src 'unsafe-inline' : les vues utilisent des attributs style pour les largeurs des graphiques.
app.Use(async (contexte, suite) =>
{
    var entetes = contexte.Response.Headers;
    entetes.XContentTypeOptions = "nosniff";
    entetes.XFrameOptions = "DENY";
    entetes["Referrer-Policy"] = "strict-origin-when-cross-origin";
    entetes["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    entetes.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; "
        + "font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    await suite();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Page d'erreur lisible pour les codes HTTP (404, 403, 429…)
app.UseStatusCodePagesWithReExecute("/Home/Introuvable", "?code={0}");
app.UseHttpsRedirection();

// Saisies lues en culture invariante (montants « 150000.50 », dates ISO des champs date) ;
// l'affichage, lui, passe par Formats (fr-FR)
var localisation = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture, Formats.Fr),
    SupportedCultures = [CultureInfo.InvariantCulture],
    SupportedUICultures = [Formats.Fr]
};
localisation.RequestCultureProviders.Clear();
app.UseRequestLocalization(localisation);
app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();

// Supervision : 200 si l'application et la base répondent, 503 sinon
app.MapHealthChecks("/sante").AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program;

public class BaseDeDonneesCheck(ApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
}
