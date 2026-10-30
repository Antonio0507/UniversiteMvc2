using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace UniversiteMvc2.Infrastructure;

// Messages de validation en français, sans avoir à les répéter sur chaque attribut des modèles
public class MessagesValidationFr : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        foreach (var attribut in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
        {
            if (MessagePersonnalise(attribut)) continue;

            attribut.ErrorMessage = attribut switch
            {
                RequiredAttribute => "« {0} » est obligatoire.",
                StringLengthAttribute { MinimumLength: > 0 } => "« {0} » doit contenir entre {2} et {1} caractères.",
                StringLengthAttribute => "« {0} » ne peut pas dépasser {1} caractères.",
                RangeAttribute => "« {0} » doit être compris entre {1} et {2}.",
                EmailAddressAttribute => "Adresse e-mail invalide.",
                PhoneAttribute => "Numéro de téléphone invalide.",
                _ => attribut.ErrorMessage
            };
        }
    }

    // Un message écrit dans le modèle est conservé. EmailAddress, Phone… exposent leur message
    // anglais par défaut dans ErrorMessage : on le reconnaît en le comparant à un attribut neuf.
    private static bool MessagePersonnalise(ValidationAttribute attribut)
    {
        if (attribut.ErrorMessageResourceType != null) return true;
        if (string.IsNullOrEmpty(attribut.ErrorMessage)) return false;
        if (attribut.GetType().GetConstructor(Type.EmptyTypes) == null) return true;

        var neuf = (ValidationAttribute)Activator.CreateInstance(attribut.GetType())!;
        return attribut.ErrorMessage != neuf.ErrorMessage;
    }

    public static void Configurer(MvcOptions options)
    {
        options.ModelMetadataDetailsProviders.Add(new MessagesValidationFr());

        var messages = options.ModelBindingMessageProvider;
        messages.SetValueMustNotBeNullAccessor(_ => "Ce champ est obligatoire.");
        messages.SetMissingBindRequiredValueAccessor(champ => $"« {champ} » est obligatoire.");
        messages.SetValueIsInvalidAccessor(valeur => $"La valeur « {valeur} » n'est pas valide.");
        messages.SetAttemptedValueIsInvalidAccessor((valeur, champ) => $"La valeur « {valeur} » n'est pas valide pour « {champ} ».");
        messages.SetUnknownValueIsInvalidAccessor(champ => $"La valeur saisie pour « {champ} » n'est pas valide.");
        messages.SetValueMustBeANumberAccessor(champ => $"« {champ} » doit être un nombre.");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "La valeur doit être un nombre.");
    }
}
