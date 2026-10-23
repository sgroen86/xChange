using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Api.Billing;

/// <summary>The problem texts of the billing endpoints.</summary>
internal interface IBillingApiText
{
    string UnavailableTitle { get; }

    string UnavailableDetail { get; }

    string InvalidCheckoutDetail { get; }

    string AlreadySubscribedTitle { get; }

    string AlreadySubscribedDetail { get; }

    string NoCustomerTitle { get; }

    string NoCustomerDetail { get; }
}

internal sealed class BillingApiTextNl : IBillingApiText
{
    public string UnavailableTitle => "Betalen lukt nu niet";

    public string UnavailableDetail => "Betalen is nog niet ingericht of tijdelijk niet bereikbaar. Probeer het later opnieuw.";

    public string InvalidCheckoutDetail => "Kies een abonnement of tegoed.";

    public string AlreadySubscribedTitle => "Al een abonnement";

    public string AlreadySubscribedDetail => "Je organisatie heeft al een abonnement. Wissel van abonnement via 'Abonnement beheren'.";

    public string NoCustomerTitle => "Nog geen abonnement";

    public string NoCustomerDetail => "Er is nog niets om te beheren: kies eerst een abonnement of koop tegoed.";
}

internal sealed class BillingApiTextEn : IBillingApiText
{
    public string UnavailableTitle => "Payment is not possible right now";

    public string UnavailableDetail => "Payments are not set up yet or temporarily unreachable. Please try again later.";

    public string InvalidCheckoutDetail => "Choose a plan or credits.";

    public string AlreadySubscribedTitle => "Already subscribed";

    public string AlreadySubscribedDetail => "Your organisation already has a plan. Switch plans through 'Manage subscription'.";

    public string NoCustomerTitle => "No plan yet";

    public string NoCustomerDetail => "There is nothing to manage yet: choose a plan or buy credits first.";
}

internal static class BillingApiText
{
    private static readonly IBillingApiText Nl = new BillingApiTextNl(), En = new BillingApiTextEn();

    public static IBillingApiText Current => Texts.Pick(Nl, En);
}
