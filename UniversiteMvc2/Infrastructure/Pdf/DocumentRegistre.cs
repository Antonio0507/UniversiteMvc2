using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UniversiteMvc2.Models;

namespace UniversiteMvc2.Infrastructure.Pdf;

// Charte des PDF : mêmes couleurs et même rythme que l'interface (filets, étiquettes, accent turquoise)
public static class Charte
{
    public const string Encre = "#0A0B19";
    public const string Encre2 = "#3A3C52";
    public const string Encre3 = "#6B6E85";
    public const string Filet = "#DDDFE6";
    public const string Accent = "#1B9A98";

    public static TextStyle Etiquette => TextStyle.Default.FontSize(7.5f).FontColor(Encre3).LetterSpacing(0.06f);

    // Les espaces insécables fines (séparateur de milliers fr-FR) deviennent des espaces simples,
    // toutes les polices ne les contenant pas
    public static string Texte(string? texte) => (texte ?? "—").Replace(' ', ' ').Replace(' ', ' ');
}

// Gabarit commun : en-tête de l'établissement, référence, titre, pied de page numéroté
public abstract class DocumentRegistre : IDocument
{
    protected readonly Etablissement Etablissement;
    protected readonly DemandeDocument Demande;
    private readonly string _titre;
    private readonly string _reference;

    protected DocumentRegistre(Etablissement etablissement, DemandeDocument demande, string titre, string reference)
    {
        Etablissement = etablissement;
        Demande = demande;
        _titre = titre;
        _reference = reference;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = _titre,
        Author = Etablissement.Nom,
        Creator = "Gestion universitaire"
    };

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

    // Grille « étiquette / valeur », comme les fiches de l'interface
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

    protected void Signature(IContainer conteneur) =>
        conteneur.AlignRight().Width(230).Column(colonne =>
        {
            colonne.Item().Text($"Fait à {Demande.Lieu}, le {Charte.Texte(Formats.DateLongue(Demande.DateDocument))}");
            colonne.Item().PaddingTop(4).Text(Demande.Signataire).SemiBold();
            colonne.Item().PaddingTop(48).LineHorizontal(0.75f).LineColor(Charte.Encre);
            colonne.Item().PaddingTop(3).Text("CACHET ET SIGNATURE").Style(Charte.Etiquette);
        });

    protected static IContainer CelluleEntete(IContainer c) =>
        c.BorderBottom(1).BorderColor(Charte.Encre).PaddingBottom(5).PaddingRight(6).DefaultTextStyle(Charte.Etiquette);

    protected static IContainer Cellule(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Charte.Filet).PaddingVertical(5).PaddingRight(6);

    protected static IContainer CelluleTotal(IContainer c) =>
        c.BorderTop(1).BorderColor(Charte.Encre).PaddingTop(6).PaddingRight(6).DefaultTextStyle(s => s.SemiBold());
}
