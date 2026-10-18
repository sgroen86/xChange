using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Domain.Learning;

/// <summary>The notices <see cref="CorrectionMemory"/> shows when it puts a learned correction back.</summary>
internal interface ICorrectionMemoryText
{
    string FilledAsCorrectedBefore(string? value);

    string LineTaxChangedForBuyer(string from, string to);

    string ExemptionReasonFilledForBuyer(string category, string? value);

    /// <summary>A tax without a category, in <see cref="LineTaxChangedForBuyer"/>.</summary>
    string None { get; }
}

internal sealed class CorrectionMemoryTextNl : ICorrectionMemoryText
{
    public string FilledAsCorrectedBefore(string? value) => $"Ingevuld zoals u bij een eerdere factuur corrigeerde: {value}.";

    public string LineTaxChangedForBuyer(string from, string to) => $"Btw van de regels aangepast zoals eerder voor deze afnemer: {from} → {to}.";

    public string ExemptionReasonFilledForBuyer(string category, string? value)
        => $"Vrijstellingsreden voor {category} ingevuld zoals eerder voor deze afnemer: {value}.";

    public string None => "geen";
}

internal sealed class CorrectionMemoryTextEn : ICorrectionMemoryText
{
    public string FilledAsCorrectedBefore(string? value) => $"Filled in as you corrected it on an earlier invoice: {value}.";

    public string LineTaxChangedForBuyer(string from, string to) => $"Line VAT changed as before for this buyer: {from} → {to}.";

    public string ExemptionReasonFilledForBuyer(string category, string? value)
        => $"Exemption reason for {category} filled in as before for this buyer: {value}.";

    public string None => "none";
}

internal static class CorrectionMemoryText
{
    private static readonly ICorrectionMemoryText Nl = new CorrectionMemoryTextNl(), En = new CorrectionMemoryTextEn();

    public static ICorrectionMemoryText Current => Texts.Pick(Nl, En);
}
