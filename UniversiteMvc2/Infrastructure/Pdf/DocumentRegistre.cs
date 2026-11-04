using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure.Pdf;

/// <summary>
/// Charte des PDF : mêmes couleurs et même rythme que l'interface (filets, étiquettes, accent turquoise)
/// </summary>
public static class Charte
{
    /// <summary>Couleur du texte principal.</summary>
    public const string Encre = "#0A0B19";
    /// <summary>Couleur du texte secondaire.</summary>
    public const string Encre2 = "#3A3C52";
    /// <summary>Couleur des étiquettes et du texte discret.</summary>
    public const string Encre3 = "#6B6E85";
    /// <summary>Couleur des filets.</summary>
    public const string Filet = "#DDDFE6";
    /// <summary>Couleur d'accent (turquoise).</summary>
    public const string Accent = "#1B9A98";

    /// <summary>Style des petites étiquettes en capitales (« MATRICULE »).</summary>
    public static TextStyle Etiquette => TextStyle.Default.FontSize(7.5f).FontColor(Encre3).LetterSpacing(0.06f);

    /// <summary>
    /// Les espaces insécables fines (séparateur de milliers fr-FR) deviennent des espaces simples, toutes les polices
    /// ne les contenant pas
    /// </summary>
    /// <param name="texte">Texte, ou null pour « — ».</param>
    public static string Texte(string? texte) => (texte ?? "—").Replace(' ', ' ').Replace(' ', ' ');
}

/// <summary>Gabarit commun : en-tête de l'établissement, référence, titre, pied de page numéroté</summary>
public abstract class DocumentRegistre : IDocument
{
    /// <summary>Identité de l'établissement.</summary>
    protected readonly Etablissement Etablissement;
    /// <summary>Demande saisie : signataire, lieu, date et critères.</summary>
    protected readonly DemandeDocument Demande;
    private readonly string _titre;
    private readonly string _reference;

    /// <summary>Prépare le gabarit.</summary>
    /// <param name="etablissement">Identité de l'établissement (en-tête et pied de page).</param>
    /// <param name="demande">Signataire, lieu, date et critères saisis dans le formulaire.</param>
    /// <param name="titre">Titre du document.</param>
    /// <param name="reference">Référence imprimée en haut à droite.</param>
    protected DocumentRegistre(Etablissement etablissement, DemandeDocument demande, string titre, string reference)
    {
        Etablissement = etablissement;
        Demande = demande;
        _titre = titre;
        _reference = reference;
    }

    /// <summary>Métadonnées du fichier PDF : titre, auteur, logiciel.</summary>
    public DocumentMetadata GetMetadata() => new()
    {
        Title = _titre,
        Author = Etablissement.Nom,
        Creator = "Gestion universitaire"
    };

    /// <summary>Met en page le document : A4, marges, en-tête, contenu, pied de page.</summary>
    /// <param name="container">Conteneur QuestPDF.</param>
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(2.2f, Unit.Centimetre);
            page.MarginVertical(1.8f, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(s => s.FontSize(10).FontColor(Charte.Encre).LineHeight(1.35f));

            page.Header().Element(EnTete);
            page.Content().PaddingTop(24).Element(Contenu);
            page.Footer().Element(PiedDePage);
        });
    }

    /// <summary>Corps du document, propre à chaque type.</summary>
    /// <param name="conteneur">Zone de contenu de la page.</param>
    protected abstract void Contenu(IContainer conteneur);

    private void EnTete(IContainer conteneur) => conteneur.Column(colonne =>
    {
        colonne.Item().Row(ligne =>
        {
            ligne.RelativeItem().Column(gauche =>
            {
                gauche.Item().Text(t =>
                {
                    t.Span(Etablissement.Nom).FontSize(16).SemiBold();
                    t.Span(".").FontSize(16).SemiBold().FontColor(Charte.Accent);
                });
                gauche.Item().PaddingTop(2).Text(Etablissement.Service.ToUpperInvariant()).Style(Charte.Etiquette);
            });
            ligne.ConstantItem(180).AlignRight().Column(droite =>
            {
                droite.Item().AlignRight().Text("RÉFÉRENCE").Style(Charte.Etiquette);
                droite.Item().AlignRight().Text(_reference).SemiBold();
            });
        });
        colonne.Item().PaddingTop(10).LineHorizontal(1.5f).LineColor(Charte.Encre);
        colonne.Item().PaddingTop(20).Text(_titre).FontSize(21).SemiBold();
    });

    private void PiedDePage(IContainer conteneur) => conteneur.Column(colonne =>
    {
        var coordonnees = new[] { Etablissement.Nom, Etablissement.Adresse, Etablissement.Ville, Etablissement.Telephone }
            .Where(s => !string.IsNullOrWhiteSpace(s));

        colonne.Item().LineHorizontal(0.5f).LineColor(Charte.Filet);
        colonne.Item().PaddingTop(6).Row(ligne =>
        {
            ligne.RelativeItem().Text(string.Join(" · ", coordonnees)).FontSize(7.5f).FontColor(Charte.Encre3);
            ligne.ConstantItem(90).AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Charte.Encre3));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    });

    // --- Éléments réutilisables ---------------------------------------------

    /// <summary>Grille « étiquette / valeur », comme les fiches de l'interface</summary>
    /// <param name="conteneur">Zone où placer la grille.</param>
    /// <param name="lignes">Paires étiquette, valeur.</param>
    /// <param name="colonnes">Nombre de colonnes.</param>
    protected static void Fiche(IContainer conteneur, IEnumerable<(string Libelle, string? Valeur)> lignes, int colonnes = 3) =>
        conteneur.BorderTop(0.5f).BorderColor(Charte.Filet).Table(table =>
        {
            table.ColumnsDefinition(d =>
            {
                for (var i = 0; i < colonnes; i++) d.RelativeColumn();
            });
            foreach (var (libelle, valeur) in lignes)
            {
                table.Cell().BorderBottom(0.5f).BorderColor(Charte.Filet).PaddingVertical(7).PaddingRight(10).Column(c =>
                {
                    c.Item().Text(libelle.ToUpperInvariant()).Style(Charte.Etiquette);
                    c.Item().PaddingTop(2).Text(Charte.Texte(valeur));
                });
            }
        });

    /// <summary>« Fait à … le … », titre du signataire et espace pour le cachet et la signature.</summary>
    /// <param name="conteneur">Zone où placer la signature.</param>
    protected void Signature(IContainer conteneur) =>
        conteneur.AlignRight().Width(230).Column(colonne =>
        {
            colonne.Item().Text($"Fait à {Demande.Lieu}, le {Charte.Texte(Formats.DateLongue(Demande.DateDocument))}");
            colonne.Item().PaddingTop(4).Text(Demande.Signataire).SemiBold();
            colonne.Item().PaddingTop(48).LineHorizontal(0.75f).LineColor(Charte.Encre);
            colonne.Item().PaddingTop(3).Text("CACHET ET SIGNATURE").Style(Charte.Etiquette);
        });

    /// <summary>Style d'une cellule d'en-tête de tableau.</summary>
    /// <param name="c">Cellule.</param>
    protected static IContainer CelluleEntete(IContainer c) =>
        c.BorderBottom(1).BorderColor(Charte.Encre).PaddingBottom(5).PaddingRight(6).DefaultTextStyle(Charte.Etiquette);

    /// <summary>Style d'une cellule de tableau.</summary>
    /// <param name="c">Cellule.</param>
    protected static IContainer Cellule(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Charte.Filet).PaddingVertical(5).PaddingRight(6);

    /// <summary>Style d'une cellule de total.</summary>
    /// <param name="c">Cellule.</param>
    protected static IContainer CelluleTotal(IContainer c) =>
        c.BorderTop(1).BorderColor(Charte.Encre).PaddingTop(6).PaddingRight(6).DefaultTextStyle(s => s.SemiBold());
}
