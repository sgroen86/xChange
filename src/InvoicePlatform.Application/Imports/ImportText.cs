using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Application.Imports;

/// <summary>
/// The messages of <see cref="ImportService"/>. An import is read by the
/// worker, outside any request, so these are in the default language there.
/// </summary>
internal interface IImportText
{
    string PdfNotFound { get; }
}

internal sealed class ImportTextNl : IImportText
{
    public string PdfNotFound => "Het PDF-bestand is niet meer gevonden.";
}

internal sealed class ImportTextEn : IImportText
{
    public string PdfNotFound => "The PDF file could no longer be found.";
}

internal static class ImportText
{
    private static readonly IImportText Nl = new ImportTextNl(), En = new ImportTextEn();

    public static IImportText Current => Texts.Pick(Nl, En);
}
