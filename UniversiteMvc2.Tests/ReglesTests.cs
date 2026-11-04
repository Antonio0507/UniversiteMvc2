using System.Security.Claims;
using System.Text;
using UniversiteMvc2.Infrastructure;
using UniversiteMvc2.Models;
using Xunit;

namespace UniversiteMvc2.Tests;

// Règles métier pures : aucune base de données nécessaire
public class ReglesTests
{
    private static ClaimsPrincipal Profil(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(12.5, null, 12.5)]
    [InlineData(null, 11.0, 11.0)]
    [InlineData(8.0, 11.0, 11.0)]
    [InlineData(14.0, 9.0, 14.0)]
    public void La_meilleure_session_est_retenue(double? valeur, double? rattrapage, double? attendu)
    {
        var note = new Note { Valeur = (decimal?)valeur, Rattrapage = (decimal?)rattrapage };
        Assert.Equal((decimal?)attendu, note.Retenue);
    }

    [Fact]
    public void Le_bilan_pondere_par_les_credits_et_ignore_les_UE_sans_note()
    {
        var bilan = Bilan.Calculer([(6, 14m), (4, 9m), (5, null)]);

        Assert.Equal(12m, bilan.Moyenne);
        Assert.Equal(6, bilan.CreditsValides);
        Assert.Equal(15, bilan.CreditsTotal);
        Assert.Equal(1, bilan.UESansNote);
    }

    [Fact]
    public void Le_bilan_sans_note_n_a_pas_de_moyenne()
    {
        Assert.Null(Bilan.Calculer([(6, null)]).Moyenne);
        Assert.Null(Bilan.Calculer([]).Moyenne);
    }

    [Fact]
    public void Une_note_de_10_valide_l_UE() => Assert.Equal(6, Bilan.Calculer([(6, 10m)]).CreditsValides);

    [Theory]
    // Notes de deux UE (6 et 4 crédits) ; null = pas de note
    [InlineData(12.0, 10.0, Decisions.Admis)]
    [InlineData(14.0, 8.0, Decisions.AdmisCompensation)]
    [InlineData(9.0, 9.5, Decisions.Ajourne)]
    [InlineData(15.0, null, Decisions.Incomplet)]
    public void La_decision_du_jury_suit_credits_compensation_et_notes_manquantes(double premiere, double? seconde, string attendu) =>
        Assert.Equal(attendu, Bilan.Calculer([(6, (decimal)premiere), (4, (decimal?)seconde)]).Decision);

    [Fact]
    public void Sans_UE_le_jury_ne_peut_pas_se_prononcer() => Assert.Equal(Decisions.Incomplet, Bilan.Calculer([]).Decision);

    [Fact]
    public void Les_ex_aequo_partagent_leur_rang_et_sans_moyenne_pas_de_rang()
    {
        var promotion = new decimal?[] { 12m, 15m, 12m, 9m, null }
            .Select(m => (new Inscription(), new Bilan(m, 0, 0, m == null ? 1 : 0)));

        var rangs = Deliberations.Classer(promotion).Select(l => l.Rang).ToList();

        Assert.Equal([2, 1, 2, 4, null], rangs);
    }

    [Fact]
    public void Les_aides_reduisent_le_du_sans_le_rendre_negatif()
    {
        Assert.Equal(250000m, Solde.Net(350000m, 100000m));
        Assert.Equal(0m, Solde.Net(100000m, 150000m));
    }

    [Theory]
    [InlineData(8, 10, 10, 12, false)]
    [InlineData(8, 10, 9, 11, true)]
    [InlineData(8, 12, 9, 10, true)]
    public void Deux_seances_le_meme_jour_se_chevauchent_si_leurs_horaires_se_croisent(int debutA, int finA, int debutB, int finB, bool attendu)
    {
        var jour = new DateTime(2026, 11, 2);
        var a = new Seance { Date = jour, HeureDebut = TimeSpan.FromHours(debutA), HeureFin = TimeSpan.FromHours(finA) };
        var b = new Seance { Date = jour, HeureDebut = TimeSpan.FromHours(debutB), HeureFin = TimeSpan.FromHours(finB) };

        Assert.Equal(attendu, a.Chevauche(b));
        Assert.False(a.Chevauche(new Seance { Date = jour.AddDays(1), HeureDebut = a.HeureDebut, HeureFin = a.HeureFin }));
    }

    [Fact]
    public void Le_reste_a_payer_ne_devient_jamais_negatif()
    {
        Assert.Equal(50m, new Solde(150m, 100m).Reste);
        Assert.Equal(0m, new Solde(100m, 150m).Reste);
        Assert.Null(new Solde(null, 100m).Reste);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+228 90", "'+228 90")]
    [InlineData("@SOMME(A1)", "'@SOMME(A1)")]
    [InlineData("-2", "'-2")]
    [InlineData("KOFFI", "KOFFI")]
    public void Le_CSV_neutralise_les_formules(string valeur, string attendu)
    {
        var texte = Encoding.UTF8.GetString(Csv.Generer(["A"], [[valeur]]));
        Assert.Equal(attendu, texte.Split(Environment.NewLine)[1]);
    }

    [Fact]
    public void Le_CSV_echappe_separateurs_et_guillemets()
    {
        var texte = Encoding.UTF8.GetString(Csv.Generer(["A"], [["a;b\"c"]]));
        Assert.Contains("\"a;b\"\"c\"", texte);
    }

    [Fact]
    public void Le_CSV_commence_par_le_BOM_pour_Excel()
    {
        var octets = Csv.Generer(["A"], []);
        Assert.Equal(Encoding.UTF8.GetPreamble(), octets[..3]);
    }

    [Theory]
    [InlineData("court1", false)]
    [InlineData("sanschiffreaucun", false)]
    [InlineData("12345678901", false)]
    [InlineData("assezlong123", true)]
    public void Politique_de_mot_de_passe(string motDePasse, bool accepte) =>
        Assert.Equal(accepte, MotsDePasse.Verifier(motDePasse) == null);

    [Fact]
    public void Les_mots_de_passe_generes_respectent_la_politique_et_different()
    {
        var a = MotsDePasse.Generer();
        Assert.Null(MotsDePasse.Verifier(a));
        Assert.NotEqual(a, MotsDePasse.Generer());
    }

    [Theory]
    // Profil, contrôleur, action, requête de lecture, autorisé
    [InlineData(Roles.Consultation, "Etudiants", "Index", true, true)]
    [InlineData(Roles.Consultation, "Etudiants", "Create", true, false)]
    [InlineData(Roles.Consultation, "Etudiants", "Exporter", true, false)]
    [InlineData(Roles.Consultation, "Etudiants", "Edit", false, false)]
    [InlineData(Roles.Consultation, "Comptes", "Index", true, false)]
    [InlineData(Roles.Consultation, "Journal", "Index", true, false)]
    [InlineData(Roles.Consultation, "Documents", "Index", false, true)]
    [InlineData(Roles.Scolarite, "Etudiants", "Create", false, true)]
    [InlineData(Roles.Scolarite, "Notes", "Index", false, true)]
    [InlineData(Roles.Scolarite, "Paiements", "Create", false, false)]
    [InlineData(Roles.Scolarite, "Tarifs", "Edit", true, false)]
    [InlineData(Roles.Comptabilite, "Paiements", "Delete", false, true)]
    [InlineData(Roles.Comptabilite, "Tarifs", "Create", true, true)]
    [InlineData(Roles.Comptabilite, "Notes", "Index", false, false)]
    [InlineData(Roles.Comptabilite, "Notes", "Index", true, true)]
    [InlineData(Roles.Administrateur, "Comptes", "Reinitialiser", false, true)]
    [InlineData(Roles.Administrateur, "Journal", "Index", true, true)]
    [InlineData(Roles.Scolarite, "Seances", "Create", true, true)]
    [InlineData(Roles.Scolarite, "Absences", "Appel", false, true)]
    [InlineData(Roles.Scolarite, "Aides", "Create", true, false)]
    [InlineData(Roles.Comptabilite, "Aides", "Create", false, true)]
    [InlineData(Roles.Comptabilite, "Absences", "Appel", false, false)]
    [InlineData(Roles.Consultation, "Absences", "Appel", true, true)]
    [InlineData(Roles.Consultation, "Deliberations", "Index", true, true)]
    [InlineData(Roles.Consultation, "Deliberations", "Exporter", true, false)]
    [InlineData(Roles.Consultation, "Salles", "Edit", true, false)]
    public void Matrice_des_droits(string role, string controleur, string action, bool lecture, bool attendu) =>
        Assert.Equal(attendu, Droits.Autorise(Profil(role), controleur, action, lecture));
}
