using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Application.Interpretation;

/// <summary>What the user is told when reading a document failed.</summary>
public interface IInterpretationText
{
    string Title { get; }

    string Message(InterpretationFailure reason);
}

internal sealed class InterpretationTextNl : IInterpretationText
{
    public string Title => "Lezen mislukt";

    public string Message(InterpretationFailure reason) => reason switch
    {
        InterpretationFailure.TooLarge => "De factuur is te groot om in één keer te lezen. Splits de PDF in kleinere delen.",
        InterpretationFailure.TimedOut => "Het lezen duurde te lang. Probeer het opnieuw.",
        InterpretationFailure.Declined => "Het model wilde dit document niet verwerken.",
        InterpretationFailure.Rejected => "Dit document kon niet worden gelezen, bijvoorbeeld omdat het beveiligd of beschadigd is of te veel pagina's heeft. Controleer de PDF.",
        InterpretationFailure.ProviderUnavailable => "De leesdienst is nu niet bereikbaar. Probeer het later opnieuw.",
        _ => "Het model gaf geen bruikbaar antwoord. Probeer het opnieuw.",
    };
}

internal sealed class InterpretationTextEn : IInterpretationText
{
    public string Title => "Reading failed";

    public string Message(InterpretationFailure reason) => reason switch
    {
        InterpretationFailure.TooLarge => "The invoice is too large to read in one go. Split the PDF into smaller parts.",
        InterpretationFailure.TimedOut => "Reading took too long. Please try again.",
        InterpretationFailure.Declined => "The model declined to process this document.",
        InterpretationFailure.Rejected => "This document could not be read, for example because it is protected, damaged or has too many pages. Check the PDF.",
        InterpretationFailure.ProviderUnavailable => "The reading service cannot be reached right now. Please try again later.",
        _ => "The model gave no usable answer. Please try again.",
    };
}

/// <summary>The catalogue for the current language (<see cref="Texts"/>).</summary>
public static class InterpretationText
{
    private static readonly IInterpretationText Nl = new InterpretationTextNl(), En = new InterpretationTextEn();

    public static IInterpretationText Current => Texts.Pick(Nl, En);
}
