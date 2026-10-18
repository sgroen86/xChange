using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Application.Usage;

/// <summary>The messages about an organisation's conversions.</summary>
internal interface IUsageText
{
    string ConversionLimitReached { get; }
}

internal sealed class UsageTextNl : IUsageText
{
    public string ConversionLimitReached => "Je gratis conversies zijn op. Kies een abonnement of koop tegoed om verder te gaan.";
}

internal sealed class UsageTextEn : IUsageText
{
    public string ConversionLimitReached => "You have used all your free conversions. Choose a plan or buy credit to continue.";
}

internal static class UsageText
{
    private static readonly IUsageText Nl = new UsageTextNl(), En = new UsageTextEn();

    public static IUsageText Current => Texts.Pick(Nl, En);
}
