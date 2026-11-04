namespace UniversiteMvc2.Infrastructure;

/// <summary>
/// Nom d'une entité tel qu'il apparaît dans les vues générées : « Nouvelle filière », « Supprimer l'étudiant »…
/// </summary>
/// <param name="singulier">Nom au singulier, en minuscules (« filière »).</param>
/// <param name="pluriel">Nom au pluriel (« filières »).</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EntiteAttribute(string singulier, string pluriel) : Attribute
{
    /// <summary>Nom au singulier, en minuscules.</summary>
    public string Singulier { get; } = singulier;
    /// <summary>Nom au pluriel, en minuscules.</summary>
    public string Pluriel { get; } = pluriel;
    /// <summary>Nom féminin : accorde les articles et les participes (« Nouvelle », « créée »).</summary>
    public bool Feminin { get; set; }

    /// <summary>Nom précédé de l'article défini, avec élision : « l'étudiant », « la filière », « le cours ».</summary>
    public string Le => Elision ? $"l'{Singulier}" : $"{(Feminin ? "la" : "le")} {Singulier}";

    /// <summary>Titre de création : « Nouvel étudiant », « Nouvelle filière », « Nouveau cours ».</summary>
    public string Nouveau => $"{(Feminin ? "Nouvelle" : Elision ? "Nouvel" : "Nouveau")} {Singulier}";

    /// <summary>« Tous les cours », « Toutes les filières ».</summary>
    public string Tous => $"{(Feminin ? "Toutes les" : "Tous les")} {Pluriel}";

    /// <summary>
    /// Accord("créé") → « créée » pour une entité féminine ; Accord("rattaché", true) → « rattachés »
    /// </summary>
    /// <param name="participe">Participe au masculin singulier.</param>
    /// <param name="pluriel">Accorder au pluriel.</param>
    public string Accord(string participe, bool pluriel = false) =>
        participe + (Feminin ? "e" : "") + (pluriel && !participe.EndsWith('s') ? "s" : "");

    private bool Elision => "aeiouyhéèêàâîôû".Contains(char.ToLowerInvariant(Singulier[0]));

    /// <summary>Lit l'attribut d'une entité ; à défaut, dérive un nom du nom de la classe.</summary>
    /// <param name="type">Type de l'entité.</param>
    public static EntiteAttribute De(Type type) =>
        type.GetCustomAttributes(typeof(EntiteAttribute), false).OfType<EntiteAttribute>().FirstOrDefault()
        ?? new EntiteAttribute(type.Name.ToLowerInvariant(), type.Name.ToLowerInvariant() + "s");
}

/// <summary>
/// Note affichée sous le titre d'un groupe de champs ([Display(GroupName = …)]) dans les formulaires générés
/// </summary>
/// <param name="nom">Nom du groupe ([Display(GroupName)]).</param>
/// <param name="note">Texte affiché sous le titre du groupe.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class GroupeAttribute(string nom, string note) : Attribute
{
    /// <summary>Nom du groupe de champs concerné.</summary>
    public string Nom { get; } = nom;
    /// <summary>Texte affiché sous le titre du groupe.</summary>
    public string Note { get; } = note;
}
