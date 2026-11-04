using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace UniversiteMvc2.Infrastructure;

/// <summary>
/// &lt;champ asp-for="Nom" /&gt; : libellé + contrôle + aide + message de validation, dans la charte du site.
/// &lt;champ pour="explorateur" /&gt; : même chose pour une propriété connue seulement à l'exécution (vues
/// génériques). Avec asp-items, le contrôle devient une liste déroulante (option vide via l'attribut "vide"). Aide,
/// exemple de saisie, bornes et type numérique sont lus dans les annotations du modèle ([Display(Description,
/// Prompt)], [Range], [DataType(Currency)]) ; les autres attributs passent au contrôle.
/// </summary>
[HtmlTargetElement("champ", Attributes = "asp-for", TagStructure = TagStructure.WithoutEndTag)]
[HtmlTargetElement("champ", Attributes = "pour", TagStructure = TagStructure.WithoutEndTag)]
public class ChampTagHelper : TagHelper
{
    private readonly IHtmlGenerator _generator;

    /// <summary>Crée le tag helper.</summary>
    /// <param name="generator">Générateur HTML standard d'ASP.NET Core.</param>
    public ChampTagHelper(IHtmlGenerator generator)
    {
        _generator = generator;
    }

    /// <summary>Propriété du modèle de la vue (attribut asp-for).</summary>
    [HtmlAttributeName("asp-for")]
    public ModelExpression? For { get; set; }

    /// <summary>Propriété connue seulement à l'exécution (attribut pour), utilisée par les vues génériques.</summary>
    [HtmlAttributeName("pour")]
    public ModelExplorer? Pour { get; set; }

    /// <summary>Choix proposés (attribut asp-items) ; le contrôle devient alors une liste déroulante.</summary>
    [HtmlAttributeName("asp-items")]
    public IEnumerable<SelectListItem>? Items { get; set; }

    /// <summary>Texte de l'option vide en tête de liste (attribut vide), par exemple « — Choisir — ».</summary>
    [HtmlAttributeName("vide")]
    public string? OptionVide { get; set; }

    /// <summary>Type du champ input (attribut type), quand celui déduit du modèle ne convient pas.</summary>
    [HtmlAttributeName("type")]
    public string? Type { get; set; }

    /// <summary>Aide affichée sous le champ (attribut aide) ; par défaut, la Description de [Display].</summary>
    [HtmlAttributeName("aide")]
    public string? Aide { get; set; }

    /// <summary>Force la mention « facultatif » (false) ou son absence (true) quand le modèle ne suffit pas</summary>
    [HtmlAttributeName("obligatoire")]
    public bool? Obligatoire { get; set; }

    /// <summary>Classes CSS ajoutées au conteneur du champ, par exemple champ--large.</summary>
    [HtmlAttributeName("class")]
    public string? Classe { get; set; }

    /// <summary>Contexte de la vue, fourni par Razor.</summary>
    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <summary>Produit le libellé, le contrôle, l'aide et l'emplacement du message d'erreur.</summary>
    /// <param name="context">Contexte du tag helper.</param>
    /// <param name="output">Balise produite.</param>
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var expression = For ?? new ModelExpression(Pour!.Metadata.PropertyName!, Pour);
        var meta = expression.Metadata;

        var attributsControle = new TagHelperAttributeList(output.Attributes) { { "class", "champ__controle" } };
        output.Attributes.Clear();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", string.IsNullOrEmpty(Classe) ? "champ" : $"champ {Classe}");

        var libelle = await ExecuterAsync(new LabelTagHelper(_generator) { For = expression, ViewContext = ViewContext },
            "label", TagMode.StartTagAndEndTag, [new("class", "champ__libelle")]);
        if (!(Obligatoire ?? meta.IsRequired))
            libelle.Content.AppendHtml("<span class=\"champ__facultatif\">facultatif</span>");

        TagHelperOutput controle;
        if (Items != null)
        {
            controle = await ExecuterAsync(new SelectTagHelper(_generator) { For = expression, Items = Items, ViewContext = ViewContext },
                "select", TagMode.StartTagAndEndTag, attributsControle);
            if (OptionVide != null)
                controle.PreContent.AppendHtml($"<option value=\"\">{System.Net.WebUtility.HtmlEncode(OptionVide)}</option>");
        }
        else
        {
            CompleterDepuisModele(meta, attributsControle);
            // InputTagHelper relit le type demandé dans les attributs de la balise
            if (Type != null) attributsControle.SetAttribute("type", Type);
            controle = await ExecuterAsync(new InputTagHelper(_generator) { For = expression, InputTypeName = Type, ViewContext = ViewContext },
                "input", TagMode.SelfClosing, attributsControle);
        }

        // Le « requis » implicite des types valeur (int, DateTime…) arrive avec un message anglais
        if (controle.Attributes.ContainsName("data-val-required"))
            controle.Attributes.SetAttribute("data-val-required", $"« {meta.GetDisplayName()} » est obligatoire.");

        var erreur = await ExecuterAsync(new ValidationMessageTagHelper(_generator) { For = expression, ViewContext = ViewContext },
            "span", TagMode.StartTagAndEndTag, [new("class", "champ__erreur")]);

        output.Content.AppendHtml(libelle);
        output.Content.AppendHtml(controle);
        var aide = Aide ?? meta.Description;
        if (!string.IsNullOrEmpty(aide))
            output.Content.AppendHtml($"<span class=\"champ__aide\">{System.Net.WebUtility.HtmlEncode(aide)}</span>");
        output.Content.AppendHtml(erreur);
    }

    private void CompleterDepuisModele(Microsoft.AspNetCore.Mvc.ModelBinding.ModelMetadata meta, TagHelperAttributeList attributs)
    {
        if (!string.IsNullOrEmpty(meta.Placeholder) && !attributs.ContainsName("placeholder"))
            attributs.SetAttribute("placeholder", meta.Placeholder);

        // Montants : saisie numérique entière (F CFA)
        if (meta.DataTypeName == nameof(DataType.Currency) && Type == null)
        {
            Type = "number";
            if (!attributs.ContainsName("step")) attributs.SetAttribute("step", "1");
            if (!attributs.ContainsName("min")) attributs.SetAttribute("min", "0");
        }

        var bornes = meta.ValidatorMetadata.OfType<RangeAttribute>().FirstOrDefault();
        if (bornes != null)
        {
            if (!attributs.ContainsName("min")) attributs.SetAttribute("min", Convert.ToString(bornes.Minimum, CultureInfo.InvariantCulture));
            if (!attributs.ContainsName("max")) attributs.SetAttribute("max", Convert.ToString(bornes.Maximum, CultureInfo.InvariantCulture));
        }
    }

    // Exécute un tag helper standard d'ASP.NET Core et renvoie son rendu
    private static async Task<TagHelperOutput> ExecuterAsync(TagHelper helper, string balise, TagMode mode, TagHelperAttributeList attributs)
    {
        var contexte = new TagHelperContext(balise, new TagHelperAttributeList(attributs), new Dictionary<object, object>(), Guid.NewGuid().ToString("N"));
        var sortie = new TagHelperOutput(balise, attributs, (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()))
        {
            TagMode = mode
        };
        helper.Init(contexte);
        await helper.ProcessAsync(contexte, sortie);
        return sortie;
    }
}
