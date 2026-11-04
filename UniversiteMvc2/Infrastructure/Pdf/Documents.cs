using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure.Pdf;

/// <summary>
/// Attestation d'inscription : certifie qu'un étudiant est inscrit pour l'année (inscription validée).
/// </summary>
public class AttestationInscription : DocumentRegistre
{
    private readonly Inscription _inscription;

    /// <summary>Prépare l'attestation.</summary>
    /// <param name="etablissement">Identité de l'établissement (en-tête et pied de page).</param>
    /// <param name="demande">Signataire, lieu, date et critères saisis dans le formulaire.</param>
    /// <param name="inscription">Inscription, avec étudiant, filière, niveau et année.</param>
    public AttestationInscription(Etablissement etablissement, DemandeDocument demande, Inscription inscription)
        : base(etablissement, demande, "Attestation d'inscription", $"ATT-{inscription.AnneeAcad?.Libelle}-{inscription.Id:00000}")
    {
        _inscription = inscription;
    }

    /// <inheritdoc/>
    protected override void Contenu(IContainer conteneur) => conteneur.Column(colonne =>
    {
        var etudiant = _inscription.Etudiant!;
        colonne.Spacing(14);

        colonne.Item().Text(t =>
        {
            t.Span($"Je soussigné(e), {Libelles.Minuscule(Demande.Signataire)}, atteste que l'étudiant(e) désigné(e) ci-dessous est régulièrement inscrit(e) à {Etablissement.Nom} au titre de l'année académique ");
            t.Span(Charte.Texte(_inscription.AnneeAcad?.Libelle)).SemiBold();
            t.Span(".");
        });

        colonne.Item().PaddingTop(4).Text(t =>
        {
            t.Span(etudiant.Nom).FontSize(17).SemiBold();
            t.Span($" {etudiant.Prenom}").FontSize(17).FontColor(Charte.Encre2);
        });

        colonne.Item().Element(c => Fiche(c,
        [
            ("Matricule", etudiant.Matricule),
            ("Né(e) le", Formats.DateLongue(etudiant.DateNaissance)),
            ("Inscrit(e) le", Formats.DateLongue(_inscription.DateInscription)),
            ("Filière", _inscription.Filiere?.Nom),
            ("Niveau", _inscription.Niveau?.Libelle),
            ("Année académique", _inscription.AnneeAcad?.Libelle)
        ]));

        colonne.Item().PaddingTop(4).Text("La présente attestation est délivrée à l'intéressé(e) pour servir et valoir ce que de droit.");
        colonne.Item().PaddingTop(28).Element(Signature);
    });
}

/// <summary>
/// Relevé des paiements : tous les versements d'une inscription, avec le total arrêté et le reste à payer.
/// </summary>
public class RelevePaiements : DocumentRegistre
{
    private readonly Inscription _inscription;
    private readonly decimal? _du;

    /// <summary>Prépare le relevé.</summary>
    /// <param name="etablissement">Identité de l'établissement (en-tête et pied de page).</param>
    /// <param name="demande">Signataire, lieu, date et critères saisis dans le formulaire.</param>
    /// <param name="inscription">Inscription, avec ses paiements.</param>
    /// <param name="du">Frais dus, aides déduites ; null sans tarif.</param>
    public RelevePaiements(Etablissement etablissement, DemandeDocument demande, Inscription inscription, decimal? du)
        : base(etablissement, demande, "Relevé des paiements", $"REL-{inscription.AnneeAcad?.Libelle}-{inscription.Id:00000}")
    {
        _inscription = inscription;
        _du = du;
    }

    /// <inheritdoc/>
    protected override void Contenu(IContainer conteneur) => conteneur.Column(colonne =>
    {
        var etudiant = _inscription.Etudiant!;
        var paiements = _inscription.Paiements.OrderBy(p => p.DatePaiement).ThenBy(p => p.Id).ToList();
        var total = paiements.Sum(p => p.Montant);
        colonne.Spacing(14);

        colonne.Item().Element(c => Fiche(c,
        [
            ("Étudiant", etudiant.NomComplet),
            ("Matricule", etudiant.Matricule),
            ("Statut de l'inscription", _inscription.Statut),
            ("Filière", _inscription.Filiere?.Nom),
            ("Niveau", _inscription.Niveau?.Libelle),
            ("Année académique", _inscription.AnneeAcad?.Libelle)
        ]));

        colonne.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(d =>
            {
                d.ConstantColumn(80);
                d.ConstantColumn(80);
                d.RelativeColumn();
                d.ConstantColumn(110);
            });
            table.Header(h =>
            {
                h.Cell().Element(CelluleEntete).Text("REÇU");
                h.Cell().Element(CelluleEntete).Text("DATE");
                h.Cell().Element(CelluleEntete).Text("MODE");
                h.Cell().Element(CelluleEntete).AlignRight().Text("MONTANT");
            });

            if (paiements.Count == 0)
                table.Cell().ColumnSpan(4).Element(Cellule).Text("Aucun versement enregistré.").Italic().FontColor(Charte.Encre3);

            foreach (var p in paiements)
            {
                table.Cell().Element(Cellule).Text(Formats.NumeroRecu(p.Id));
                table.Cell().Element(Cellule).Text(Formats.Date(p.DatePaiement));
                table.Cell().Element(Cellule).Text(Charte.Texte(p.Mode));
                table.Cell().Element(Cellule).AlignRight().Text(Charte.Texte(Formats.Montant(p.Montant)));
            }

            table.Cell().ColumnSpan(3).Element(CelluleTotal).Text($"Total ({Formats.Pluriel(paiements.Count, "versement")})");
            table.Cell().Element(CelluleTotal).AlignRight().Text(Charte.Texte(Formats.Montant(total)));
        });

        colonne.Item().PaddingTop(6).Text(t =>
        {
            t.Span("Arrêté le présent relevé à la somme de ");
            t.Span(Charte.Texte(Formats.Montant(total))).SemiBold();
            t.Span(".");
            if (new Solde(_du, total).Reste is { } reste)
            {
                t.Span($" Frais de scolarité (aides déduites) : {Charte.Texte(Formats.Montant(_du!.Value))}. Reste à payer : ");
                t.Span(Charte.Texte(Formats.Montant(reste))).SemiBold();
                t.Span(".");
            }
        });
        colonne.Item().PaddingTop(24).Element(Signature);
    });
}

/// <summary>Liste des inscrits d'une année, avec filtres facultatifs.</summary>
public class ListeInscrits : DocumentRegistre
{
    private readonly IReadOnlyList<Inscription> _inscriptions;
    private readonly string _criteres;

    /// <summary>Prépare la liste.</summary>
    /// <param name="etablissement">Identité de l'établissement (en-tête et pied de page).</param>
    /// <param name="demande">Signataire, lieu, date et critères saisis dans le formulaire.</param>
    /// <param name="inscriptions">Inscriptions à lister, déjà triées.</param>
    /// <param name="criteres">Critères appliqués, imprimés sous le titre.</param>
    public ListeInscrits(Etablissement etablissement, DemandeDocument demande, IReadOnlyList<Inscription> inscriptions, string criteres)
        : base(etablissement, demande, "Liste des inscrits", $"LST-{demande.DateDocument:yyyyMMdd}")
    {
        _inscriptions = inscriptions;
        _criteres = criteres;
    }

    /// <inheritdoc/>
    protected override void Contenu(IContainer conteneur) => conteneur.Column(colonne =>
    {
        colonne.Spacing(12);
        colonne.Item().Text(_criteres).FontColor(Charte.Encre2);

        colonne.Item().Table(table =>
        {
            table.ColumnsDefinition(d =>
            {
                d.ConstantColumn(24);
                d.ConstantColumn(78);
                d.RelativeColumn(3);
                d.RelativeColumn(2);
                d.ConstantColumn(32);
                d.ConstantColumn(60);
                d.ConstantColumn(78);
            });
            table.Header(h =>
            {
                h.Cell().Element(CelluleEntete).Text("N°");
                h.Cell().Element(CelluleEntete).Text("MATRICULE");
                h.Cell().Element(CelluleEntete).Text("NOM ET PRÉNOM");
                h.Cell().Element(CelluleEntete).Text("FILIÈRE");
                h.Cell().Element(CelluleEntete).Text("NIV.");
                h.Cell().Element(CelluleEntete).Text("STATUT");
                h.Cell().Element(CelluleEntete).AlignRight().Text("VERSÉ");
            });

            var numero = 0;
            foreach (var i in _inscriptions)
            {
                table.Cell().Element(Cellule).Text($"{++numero}").FontColor(Charte.Encre3);
                table.Cell().Element(Cellule).Text(Charte.Texte(i.Etudiant?.Matricule));
                table.Cell().Element(Cellule).Text(t =>
                {
                    t.Span(Charte.Texte(i.Etudiant?.Nom)).SemiBold();
                    t.Span($" {i.Etudiant?.Prenom}");
                });
                table.Cell().Element(Cellule).Text(Charte.Texte(i.Filiere?.Nom));
                table.Cell().Element(Cellule).Text(Charte.Texte(i.Niveau?.Code));
                table.Cell().Element(Cellule).Text(Charte.Texte(i.Statut));
                table.Cell().Element(Cellule).AlignRight().Text(Charte.Texte(Formats.Nombre(i.Paiements.Sum(p => p.Montant))));
            }

            table.Cell().ColumnSpan(6).Element(CelluleTotal).Text(Formats.Pluriel(_inscriptions.Count, "inscrit"));
            table.Cell().Element(CelluleTotal).AlignRight().Text(Charte.Texte(Formats.Nombre(_inscriptions.SelectMany(i => i.Paiements).Sum(p => p.Montant))));
        });

        colonne.Item().Text($"Montants en {Formats.Devise}.").FontSize(8).FontColor(Charte.Encre3);
        colonne.Item().PaddingTop(20).Element(Signature);
    });
}

/// <summary>Reçu d'un versement, avec le cumul versé et le reste à payer à cette date.</summary>
public class RecuPaiement : DocumentRegistre
{
    private readonly Paiement _paiement;
    private readonly decimal? _du;

    /// <summary>Prépare le reçu.</summary>
    /// <param name="etablissement">Identité de l'établissement (en-tête et pied de page).</param>
    /// <param name="demande">Signataire, lieu, date et critères saisis dans le formulaire.</param>
    /// <param name="paiement">Paiement, avec son inscription et tous ses versements.</param>
    /// <param name="du">Frais dus, aides déduites ; null sans tarif.</param>
    public RecuPaiement(Etablissement etablissement, DemandeDocument demande, Paiement paiement, decimal? du)
        : base(etablissement, demande, "Reçu de paiement", Formats.NumeroRecu(paiement.Id))
    {
        _paiement = paiement;
        _du = du;
    }

    /// <inheritdoc/>
    protected override void Contenu(IContainer conteneur) => conteneur.Column(colonne =>
    {
        var inscription = _paiement.Inscription!;
        // Versements arrivés jusqu'à celui-ci ; à date égale, l'ordre de saisie départage
        var jusquIci = inscription.Paiements
            .Where(p => p.DatePaiement < _paiement.DatePaiement || (p.DatePaiement == _paiement.DatePaiement && p.Id <= _paiement.Id))
            .ToList();
        var cumul = jusquIci.Sum(p => p.Montant);
        colonne.Spacing(10);

        colonne.Item().Text("REÇU DE").Style(Charte.Etiquette);
        colonne.Item().Text(t =>
        {
            t.Span(Charte.Texte(inscription.Etudiant?.Nom)).FontSize(17).SemiBold();
            t.Span($" {inscription.Etudiant?.Prenom}").FontSize(17).FontColor(Charte.Encre2);
        });
        colonne.Item().Text(Charte.Texte(inscription.Etudiant?.Matricule)).FontColor(Charte.Encre3);

        colonne.Item().PaddingTop(18).Text(Charte.Texte(Formats.Montant(_paiement.Montant))).FontSize(30).SemiBold();
        colonne.Item().Text($"versés le {Charte.Texte(Formats.DateLongue(_paiement.DatePaiement))} · {_paiement.Mode}").FontColor(Charte.Encre2);

        colonne.Item().PaddingTop(14).Element(c => Fiche(c,
        [
            ("Année académique", inscription.AnneeAcad?.Libelle),
            ("Filière", inscription.Filiere?.Nom),
            ("Niveau", inscription.Niveau?.Libelle),
            ("Cumul versé à cette date", Formats.Montant(cumul)),
            ("Nombre de versements", jusquIci.Count.ToString()),
            ("Statut de l'inscription", inscription.Statut),
            ("Frais dus (aides déduites)", _du == null ? "—" : Formats.Montant(_du.Value)),
            ("Reste à payer à cette date", new Solde(_du, cumul).Reste is { } reste ? Formats.Montant(reste) : "—")
        ]));

        colonne.Item().PaddingTop(28).Element(Signature);
    });
}

/// <summary>Relevé de notes : chaque UE du cursus, moyenne pondérée et crédits validés (inscription validée).</summary>
public class ReleveNotes : DocumentRegistre
{
    private readonly Inscription _inscription;
    private readonly IReadOnlyList<UE> _ues;

    /// <summary>Prépare le relevé.</summary>
    /// <param name="etablissement">Identité de l'établissement (en-tête et pied de page).</param>
    /// <param name="demande">Signataire, lieu, date et critères saisis dans le formulaire.</param>
    /// <param name="inscription">Inscription, avec ses notes.</param>
    /// <param name="ues">UE de la filière et du niveau, triées par code.</param>
    public ReleveNotes(Etablissement etablissement, DemandeDocument demande, Inscription inscription, IReadOnlyList<UE> ues)
        : base(etablissement, demande, "Relevé de notes", $"NOT-{inscription.AnneeAcad?.Libelle}-{inscription.Id:00000}")
    {
        _inscription = inscription;
        _ues = ues;
    }

    /// <inheritdoc/>
    protected override void Contenu(IContainer conteneur) => conteneur.Column(colonne =>
    {
        var etudiant = _inscription.Etudiant!;
        var lignes = _ues.Select(u => (UE: u, Note: _inscription.Notes.FirstOrDefault(n => n.UEId == u.Id))).ToList();
        var bilan = Bilan.Calculer(lignes.Select(l => (l.UE.Credits, l.Note?.Retenue)).ToList());
        colonne.Spacing(14);

        colonne.Item().Element(c => Fiche(c,
        [
            ("Étudiant", etudiant.NomComplet),
            ("Matricule", etudiant.Matricule),
            ("Né(e) le", Formats.DateLongue(etudiant.DateNaissance)),
            ("Filière", _inscription.Filiere?.Nom),
            ("Niveau", _inscription.Niveau?.Libelle),
            ("Année académique", _inscription.AnneeAcad?.Libelle)
        ]));

        colonne.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(d =>
            {
                d.ConstantColumn(62);
                d.RelativeColumn();
                d.ConstantColumn(44);
                d.ConstantColumn(58);
                d.ConstantColumn(58);
                d.ConstantColumn(58);
            });
            table.Header(h =>
            {
                h.Cell().Element(CelluleEntete).Text("CODE");
                h.Cell().Element(CelluleEntete).Text("UNITÉ D'ENSEIGNEMENT");
                h.Cell().Element(CelluleEntete).AlignRight().Text("CRÉD.");
                h.Cell().Element(CelluleEntete).AlignRight().Text("SESSION 1");
                h.Cell().Element(CelluleEntete).AlignRight().Text("RATTR.");
                h.Cell().Element(CelluleEntete).AlignRight().Text("RETENUE");
            });

            foreach (var (ue, note) in lignes)
            {
                table.Cell().Element(Cellule).Text(ue.Code);
                table.Cell().Element(Cellule).Text(Charte.Texte(ue.Intitule));
                table.Cell().Element(Cellule).AlignRight().Text(ue.Credits.ToString());
                table.Cell().Element(Cellule).AlignRight().Text(Formats.Note(note?.Valeur));
                table.Cell().Element(Cellule).AlignRight().Text(Formats.Note(note?.Rattrapage));
                table.Cell().Element(Cellule).AlignRight().Text(Formats.Note(note?.Retenue)).SemiBold();
            }

            table.Cell().ColumnSpan(5).Element(CelluleTotal).Text("Moyenne pondérée par les crédits");
            table.Cell().Element(CelluleTotal).AlignRight().Text(Formats.Note(bilan.Moyenne));
        });

        colonne.Item().PaddingTop(6).Text(t =>
        {
            t.Span("Crédits validés : ");
            t.Span($"{bilan.CreditsValides} / {bilan.CreditsTotal}").SemiBold();
            t.Span($" (une UE est validée à partir de {Formats.Note(Note.Seuil)}/20).");
            if (bilan.UESansNote > 0)
                t.Span($" Relevé provisoire : {Formats.Pluriel(bilan.UESansNote, "UE")} sans note.");
        });
        colonne.Item().PaddingTop(24).Element(Signature);
    });
}

/// <summary>
/// Procès-verbal de délibération d'une promotion : effectif, taux d'admis, puis rang, moyenne, crédits et décision de
/// chaque étudiant.
/// </summary>
public class ProcesVerbal : DocumentRegistre
{
    private readonly AnneeAcad _annee;
    private readonly Filiere _filiere;
    private readonly Niveau _niveau;
    private readonly IReadOnlyList<LigneDeliberation> _lignes;

    /// <summary>Prépare le procès-verbal.</summary>
    /// <param name="etablissement">Identité de l'établissement (en-tête et pied de page).</param>
    /// <param name="demande">Signataire, lieu, date et critères saisis dans le formulaire.</param>
    /// <param name="annee">Année académique.</param>
    /// <param name="filiere">Filière.</param>
    /// <param name="niveau">Niveau.</param>
    /// <param name="lignes">Résultats calculés par Deliberations.CalculerAsync.</param>
    public ProcesVerbal(Etablissement etablissement, DemandeDocument demande, AnneeAcad annee, Filiere filiere, Niveau niveau, IReadOnlyList<LigneDeliberation> lignes)
        : base(etablissement, demande, "Procès-verbal de délibération", $"PV-{annee.Libelle}-{filiere.Code}-{niveau.Code}")
    {
        _annee = annee;
        _filiere = filiere;
        _niveau = niveau;
        _lignes = lignes;
    }

    /// <inheritdoc/>
    protected override void Contenu(IContainer conteneur) => conteneur.Column(colonne =>
    {
        var admis = _lignes.Count(l => l.Bilan.Admis);
        colonne.Spacing(14);

        colonne.Item().Element(c => Fiche(c,
        [
            ("Année académique", _annee.Libelle),
            ("Filière", _filiere.Nom),
            ("Niveau", _niveau.Libelle),
            ("Étudiants délibérés", _lignes.Count.ToString()),
            ("Admis", $"{admis} ({(admis * 100.0 / _lignes.Count).ToString("0", Formats.Fr)} %)"),
            ("Ajournés / incomplets", (_lignes.Count - admis).ToString())
        ]));

        colonne.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(d =>
            {
                d.ConstantColumn(34);
                d.ConstantColumn(78);
                d.RelativeColumn();
                d.ConstantColumn(52);
                d.ConstantColumn(52);
                d.ConstantColumn(104);
            });
            table.Header(h =>
            {
                h.Cell().Element(CelluleEntete).Text("RANG");
                h.Cell().Element(CelluleEntete).Text("MATRICULE");
                h.Cell().Element(CelluleEntete).Text("ÉTUDIANT");
                h.Cell().Element(CelluleEntete).AlignRight().Text("MOY.");
                h.Cell().Element(CelluleEntete).AlignRight().Text("CRÉD.");
                h.Cell().Element(CelluleEntete).Text("DÉCISION");
            });

            foreach (var l in _lignes)
            {
                table.Cell().Element(Cellule).Text(l.Rang?.ToString() ?? "—");
                table.Cell().Element(Cellule).Text(Charte.Texte(l.Inscription.Etudiant?.Matricule));
                table.Cell().Element(Cellule).Text(Charte.Texte(l.Inscription.Etudiant?.NomComplet));
                table.Cell().Element(Cellule).AlignRight().Text(Formats.Note(l.Bilan.Moyenne));
                table.Cell().Element(Cellule).AlignRight().Text($"{l.Bilan.CreditsValides}/{l.Bilan.CreditsTotal}");
                table.Cell().Element(Cellule).Text(l.Bilan.Decision).SemiBold();
            }
        });

        colonne.Item().PaddingTop(6).Text(
            $"Une UE est validée à partir de {Formats.Note(Note.Seuil)}/20. Sans toutes ses UE, un étudiant est admis par compensation " +
            $"si sa moyenne pondérée atteint {Formats.Note(Note.Seuil)}/20. « Incomplet » : au moins une UE sans note.")
            .FontSize(8.5f).FontColor(Charte.Encre3);
        colonne.Item().PaddingTop(24).Element(Signature);
    });
}
