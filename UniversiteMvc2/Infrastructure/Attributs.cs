namespace UniversiteMvc2.Infrastructure;

// Nom d'une entité tel qu'il apparaît dans les vues générées : « Nouvelle filière », « Supprimer l'étudiant »…
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EntiteAttribute(string singulier, string pluriel) : Attribute
{
    public string Singulier { get; } = singulier;
    public string Pluriel { get; } = pluriel;
    public bool Feminin { get; set; }

    public string Le => Elision ? $"l'{Singulier}" : $"{(Feminin ? "la" : "le")} {Singulier}";

    public string Nouveau => $"{(Feminin ? "Nouvelle" : Elision ? "Nouvel" : "Nouveau")} {Singulier}";

    public string Tous => $"{(Feminin ? "Toutes les" : "Tous les")} {Pluriel}";

    // Accord("créé") → « créée » pour une entité féminine ; Accord("rattaché", true) → « rattachés »
    public string Accord(string participe, bool pluriel = false) =>
        participe + (Feminin ? "e" : "") + (pluriel && !participe.EndsWith('s') ? "s" : "");

    private bool Elision => "aeiouyhéèêàâîôû".Contains(char.ToLowerInvariant(Singulier[0]));

    public static EntiteAttribute De(Type type) =>
        type.GetCustomAttributes(typeof(EntiteAttribute), false).OfType<EntiteAttribute>().FirstOrDefault()
        ?? new EntiteAttribute(type.Name.ToLowerInvariant(), type.Name.ToLowerInvariant() + "s");
}

// Note affichée sous le titre d'un groupe de champs ([Display(GroupName = …)]) dans les formulaires générés
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class GroupeAttribute(string nom, string note) : Attribute
{
    public string Nom { get; } = nom;
    public string Note { get; } = note;
}
