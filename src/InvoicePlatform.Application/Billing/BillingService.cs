using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Usage;
using Microsoft.Extensions.Logging;

namespace InvoicePlatform.Application.Billing;

/// <param name="AppUrl">Where the app lives; checkout and the portal return to its settings page.</param>
public sealed record BillingSettings(string AppUrl);

/// <summary>
/// Plans, credits and the organisation's billing state.
///
/// Only webhooks change that state. Returning from checkout proves nothing -
/// anyone can open the success URL - so the screen polls until the webhook
/// has arrived.
/// </summary>
public sealed class BillingService(
    IBillingStore store,
    IBillingProvider provider,
    IUsageMeter usage,
    IPlatformStore platform,
    BillingSettings settings,
    ILogger<BillingService> logger)
{
    public async Task<BillingStatus> GetStatusAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var account = await store.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var summary = await usage.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        return new BillingStatus(account, summary);
    }

    /// <returns>Null when payments are not available.</returns>
    public async Task<PlanOffer?> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BillingPrice> prices;
        try
        {
            prices = await provider.GetPricesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BillingUnavailableException exception)
        {
            logger.LogWarning(exception, "Prices could not be read");
            return null;
        }

        BillingPrice? PriceOf(string lookupKey) => prices.FirstOrDefault(p => p.LookupKey == lookupKey);

        return new PlanOffer(
            [.. BillingCatalogue.Plans.Select(plan => new OfferedPlan(plan, PriceOf(plan.LookupKey)))],
            BillingCatalogue.Credits,
            PriceOf(BillingCatalogue.Credits.LookupKey));
    }

    /// <param name="planId">The plan to subscribe to; null with <paramref name="credits"/>.</param>
    /// <param name="credits">True to buy a credit pack instead.</param>
    public async Task<CheckoutResult> StartCheckoutAsync(
        string organizationId,
        string email,
        string? planId,
        bool credits,
        CancellationToken cancellationToken = default)
    {
        var plan = BillingCatalogue.FindPlan(planId);
        if (credits == (planId is not null) || (planId is not null && plan is null))
        {
            return new CheckoutResult(CheckoutOutcome.Invalid);
        }

        var account = await store.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);

        // One subscription per organisation; changing plan is the portal's job,
        // where the provider prorates. A second checkout would bill twice.
        if (plan is not null && account.SubscriptionId is not null && SubscriptionStatuses.GivesConversions(account.Status))
        {
            return new CheckoutResult(CheckoutOutcome.AlreadySubscribed);
        }

        try
        {
            var customerId = account.CustomerId ?? await CreateCustomerAsync(organizationId, email, cancellationToken).ConfigureAwait(false);
            var url = await provider.CreateCheckoutSessionAsync(
                new CheckoutSessionRequest(
                    organizationId,
                    customerId,
                    plan?.LookupKey ?? BillingCatalogue.Credits.LookupKey,
                    plan is null ? CheckoutMode.Payment : CheckoutMode.Subscription,
                    SuccessUrl: $"{settings.AppUrl}/settings/?billing=success",
                    CancelUrl: $"{settings.AppUrl}/settings/?billing=cancel"),
                cancellationToken).ConfigureAwait(false);
            return new CheckoutResult(CheckoutOutcome.Started, url);
        }
        catch (BillingUnavailableException exception)
        {
            logger.LogWarning(exception, "Checkout for organisation {OrganizationId} could not be started", organizationId);
            return new CheckoutResult(CheckoutOutcome.Unavailable);
        }
    }

    public async Task<CheckoutResult> StartPortalAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var account = await store.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (account.CustomerId is null)
        {
            return new CheckoutResult(CheckoutOutcome.NoCustomer);
        }

        try
        {
            var url = await provider.CreatePortalSessionAsync(account.CustomerId, $"{settings.AppUrl}/settings/", cancellationToken)
                .ConfigureAwait(false);
            return new CheckoutResult(CheckoutOutcome.Started, url);
        }
        catch (BillingUnavailableException exception)
        {
            logger.LogWarning(exception, "Portal for organisation {OrganizationId} could not be opened", organizationId);
            return new CheckoutResult(CheckoutOutcome.Unavailable);
        }
    }

    public async Task<WebhookOutcome> HandleWebhookAsync(string payload, string? signatureHeader, CancellationToken cancellationToken = default)
    {
        var parsed = await provider.ParseWebhookAsync(payload, signatureHeader, cancellationToken).ConfigureAwait(false);
        switch (parsed.Status)
        {
            case WebhookParseStatus.NotConfigured:
                return WebhookOutcome.NotConfigured;
            case WebhookParseStatus.InvalidSignature:
                return WebhookOutcome.InvalidSignature;
        }

        return parsed.Event switch
        {
            CheckoutCompleted e => await OnCheckoutAsync(e, cancellationToken).ConfigureAwait(false),
            SubscriptionChanged e => await OnSubscriptionAsync(e, cancellationToken).ConfigureAwait(false),
            InvoicePaid e => await OnInvoicePaidAsync(e, cancellationToken).ConfigureAwait(false),
            InvoicePaymentFailed e => await OnPaymentFailedAsync(e, cancellationToken).ConfigureAwait(false),
            _ => WebhookOutcome.Ignored,
        };
    }

    private async Task<string> CreateCustomerAsync(string organizationId, string email, CancellationToken cancellationToken)
    {
        var organization = await platform.FindOrganizationAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var created = await provider.CreateCustomerAsync(organizationId, organization?.Name ?? organizationId, email, cancellationToken)
            .ConfigureAwait(false);

        // Two admins at once: the first link wins, the other customer is never used.
        return await store.LinkCustomerAsync(organizationId, created, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WebhookOutcome> OnCheckoutAsync(CheckoutCompleted e, CancellationToken cancellationToken)
    {
        var organizationId = await ResolveAsync(e.CustomerId, e.OrganizationId, e, cancellationToken).ConfigureAwait(false);
        if (organizationId is null)
        {
            return WebhookOutcome.Ignored;
        }

        if (e.CustomerId is not null)
        {
            await store.LinkCustomerAsync(organizationId, e.CustomerId, cancellationToken).ConfigureAwait(false);
        }

        if (!e.OneOffPayment || !e.Paid || e.LookupKey != BillingCatalogue.Credits.LookupKey)
        {
            // A subscription's conversions come with its own events; an unpaid
            // one-off payment waits for async_payment_succeeded.
            return WebhookOutcome.Applied;
        }

        // Keyed by the checkout session, not the event: "completed" and a later
        // "async payment succeeded" of the same purchase credit it once.
        var written = await store.AddCreditsAsync(
            organizationId, BillingCatalogue.Credits.Credits, $"checkout:{e.SessionId}", cancellationToken).ConfigureAwait(false);
        return Outcome(written);
    }

    private async Task<WebhookOutcome> OnSubscriptionAsync(SubscriptionChanged e, CancellationToken cancellationToken)
    {
        var organizationId = await ResolveAsync(e.CustomerId, e.OrganizationId, e, cancellationToken).ConfigureAwait(false);
        if (organizationId is null)
        {
            return WebhookOutcome.Ignored;
        }

        var plan = BillingCatalogue.FindPlanByLookupKey(e.LookupKey);
        if (plan is null && SubscriptionStatuses.GivesConversions(e.Status))
        {
            logger.LogError(
                "Subscription {SubscriptionId} of organisation {OrganizationId} has price {LookupKey}, which is not a plan; it gives no conversions",
                e.SubscriptionId, organizationId, e.LookupKey);
        }

        var gives = plan is not null && SubscriptionStatuses.GivesConversions(e.Status);
        var written = await store.ApplySubscriptionAsync(
            organizationId,
            new SubscriptionState(
                e.SubscriptionId,
                gives ? plan!.Id : null,
                e.Status,
                gives ? plan!.MonthlyConversions : null,
                e.PeriodStart,
                e.PeriodEnd,
                e.CancelAtPeriodEnd),
            e.Created,
            e.EventId,
            cancellationToken).ConfigureAwait(false);
        return Outcome(written);
    }

    private async Task<WebhookOutcome> OnInvoicePaidAsync(InvoicePaid e, CancellationToken cancellationToken)
    {
        // Only the first invoice and each renewal start a period; a proration
        // after a plan change does not.
        if (e.SubscriptionId is null || e.BillingReason is not ("subscription_create" or "subscription_cycle"))
        {
            return WebhookOutcome.Ignored;
        }

        var organizationId = await ResolveAsync(e.CustomerId, e.OrganizationId, e, cancellationToken).ConfigureAwait(false);
        if (organizationId is null)
        {
            return WebhookOutcome.Ignored;
        }

        var written = await store.StartPeriodAsync(organizationId, e.PeriodStart ?? e.Created, e.EventId, cancellationToken)
            .ConfigureAwait(false);
        return Outcome(written);
    }

    private async Task<WebhookOutcome> OnPaymentFailedAsync(InvoicePaymentFailed e, CancellationToken cancellationToken)
    {
        if (e.SubscriptionId is null)
        {
            return WebhookOutcome.Ignored;
        }

        var organizationId = await ResolveAsync(e.CustomerId, e.OrganizationId, e, cancellationToken).ConfigureAwait(false);
        if (organizationId is null)
        {
            return WebhookOutcome.Ignored;
        }

        var written = await store.MarkPastDueAsync(organizationId, e.SubscriptionId, e.Created, e.EventId, cancellationToken)
            .ConfigureAwait(false);
        return Outcome(written);
    }

    /// <summary>
    /// The organisation an event is about: by the customer xChange linked, or
    /// else by the organisation id xChange itself put in the metadata. Nothing
    /// else in an event is trusted to name a tenant.
    /// </summary>
    private async Task<string?> ResolveAsync(string? customerId, string? metadataOrganizationId, BillingEvent e, CancellationToken cancellationToken)
    {
        var linked = customerId is null
            ? null
            : await store.FindOrganizationByCustomerAsync(customerId, cancellationToken).ConfigureAwait(false);

        if (linked is not null && metadataOrganizationId is not null && linked != metadataOrganizationId)
        {
            logger.LogError(
                "Event {EventId}: customer {CustomerId} belongs to {Linked}, metadata says {Metadata}; using the customer's",
                e.EventId, customerId, linked, metadataOrganizationId);
        }

        var organizationId = linked ?? (string.IsNullOrWhiteSpace(metadataOrganizationId) ? null : metadataOrganizationId);
        if (organizationId is null)
        {
            logger.LogWarning("Event {EventId} ({Type}) names no organisation xChange knows; ignored", e.EventId, e.GetType().Name);
        }

        return organizationId;
    }

    private static WebhookOutcome Outcome(BillingWrite written) => written switch
    {
        BillingWrite.Applied => WebhookOutcome.Applied,
        BillingWrite.Duplicate => WebhookOutcome.Duplicate,
        _ => WebhookOutcome.Ignored,
    };
}

/// <summary>What the settings page shows.</summary>
public sealed record BillingStatus(BillingAccount Account, UsageSummary Usage)
{
    /// <summary>May download (and later send) e-invoices; see <see cref="ExportEntitlement"/>.</summary>
    public bool CanExport => ExportEntitlement.Allows(Account, Usage);
}

public sealed record OfferedPlan(BillingPlan Plan, BillingPrice? Price);

/// <param name="CreditsPrice">Null when the provider has no active price for it.</param>
public sealed record PlanOffer(IReadOnlyList<OfferedPlan> Plans, CreditPack Credits, BillingPrice? CreditsPrice);

public enum CheckoutOutcome
{
    Started,

    /// <summary>Neither or both of plan and credits, or an unknown plan.</summary>
    Invalid,

    /// <summary>There is a running subscription; plans change in the portal.</summary>
    AlreadySubscribed,

    /// <summary>The portal needs a customer, which the first checkout creates.</summary>
    NoCustomer,

    /// <summary>Payments are not set up, or the provider failed.</summary>
    Unavailable,
}

/// <param name="Url">Where to send the browser, when started.</param>
public sealed record CheckoutResult(CheckoutOutcome Outcome, string? Url = null);

public enum WebhookOutcome
{
    NotConfigured,
    InvalidSignature,
    Applied,
    Duplicate,
    Ignored,
}
