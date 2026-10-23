namespace InvoicePlatform.Application.Billing;

/// <summary>
/// The payment provider, as far as xChange needs it. Implemented with Stripe
/// in Infrastructure; no Stripe type crosses this interface.
/// </summary>
public interface IBillingProvider
{
    /// <summary>One customer per organisation, carrying its id in metadata.</summary>
    /// <returns>The provider's customer id.</returns>
    /// <exception cref="BillingUnavailableException">Not configured, or the provider failed.</exception>
    Task<string> CreateCustomerAsync(string organizationId, string organizationName, string email, CancellationToken cancellationToken = default);

    /// <returns>The URL of the hosted checkout page.</returns>
    /// <exception cref="BillingUnavailableException">Not configured, the price is unknown, or the provider failed.</exception>
    Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken = default);

    /// <returns>The URL of the hosted portal, where a customer manages their subscription.</returns>
    /// <exception cref="BillingUnavailableException">Not configured, or the provider failed.</exception>
    Task<string> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken = default);

    /// <summary>The current prices of <see cref="BillingCatalogue.LookupKeys"/>; a key without an active price is left out.</summary>
    /// <exception cref="BillingUnavailableException">Not configured, or the provider failed.</exception>
    Task<IReadOnlyList<BillingPrice>> GetPricesAsync(CancellationToken cancellationToken = default);

    /// <summary>Checks a webhook's signature and turns it into one of our own events.</summary>
    Task<WebhookParseResult> ParseWebhookAsync(string payload, string? signatureHeader, CancellationToken cancellationToken = default);
}

public enum CheckoutMode
{
    /// <summary>A monthly plan.</summary>
    Subscription,

    /// <summary>A one-off purchase (credits).</summary>
    Payment,
}

/// <param name="OrganizationId">Goes into client_reference_id and metadata, for the webhooks.</param>
/// <param name="LookupKey">The price to sell, by its lookup key.</param>
public sealed record CheckoutSessionRequest(
    string OrganizationId,
    string CustomerId,
    string LookupKey,
    CheckoutMode Mode,
    string SuccessUrl,
    string CancelUrl);

/// <param name="Amount">Per month for a plan, for the whole pack for credits; excluding VAT.</param>
/// <param name="Currency">ISO 4217, upper case.</param>
public sealed record BillingPrice(string LookupKey, decimal Amount, string Currency);

/// <summary>Payments are not set up yet, or the provider could not be reached.</summary>
public sealed class BillingUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public enum WebhookParseStatus
{
    /// <summary>No webhook secret yet: nothing can be verified, so nothing is accepted.</summary>
    NotConfigured,

    /// <summary>Missing, wrong or expired signature, or not an event.</summary>
    InvalidSignature,

    Parsed,
}

public sealed record WebhookParseResult(WebhookParseStatus Status, BillingEvent? Event = null)
{
    public static readonly WebhookParseResult NotConfigured = new(WebhookParseStatus.NotConfigured);

    public static readonly WebhookParseResult InvalidSignature = new(WebhookParseStatus.InvalidSignature);
}

/// <summary>A verified provider event, in xChange's terms.</summary>
public abstract record BillingEvent(string EventId, DateTimeOffset Created);

/// <summary>
/// A checkout finished (or, for a payment method that confirms later, its
/// payment succeeded).
/// </summary>
/// <param name="OrganizationId">From client_reference_id or metadata, both set by xChange.</param>
/// <param name="OneOffPayment">Mode "payment" rather than "subscription".</param>
/// <param name="Paid">payment_status = paid.</param>
/// <param name="LookupKey">What was bought, from the session's metadata.</param>
public sealed record CheckoutCompleted(
    string EventId,
    DateTimeOffset Created,
    string SessionId,
    string? CustomerId,
    string? OrganizationId,
    bool OneOffPayment,
    bool Paid,
    string? LookupKey) : BillingEvent(EventId, Created);

/// <summary>A subscription was created, changed or ended.</summary>
/// <param name="Status">The provider's status: active, trialing, past_due, canceled, unpaid, incomplete, ...</param>
/// <param name="LookupKey">The lookup key of the subscription's price.</param>
/// <param name="OrganizationId">From the subscription's metadata, set by xChange at checkout.</param>
public sealed record SubscriptionChanged(
    string EventId,
    DateTimeOffset Created,
    string? CustomerId,
    string SubscriptionId,
    string Status,
    string? LookupKey,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    bool CancelAtPeriodEnd,
    string? OrganizationId) : BillingEvent(EventId, Created);

/// <summary>An invoice was paid.</summary>
/// <param name="BillingReason">subscription_create, subscription_cycle, subscription_update, manual, ...</param>
/// <param name="PeriodStart">Start of the period the invoice's (first) line covers.</param>
public sealed record InvoicePaid(
    string EventId,
    DateTimeOffset Created,
    string? CustomerId,
    string? SubscriptionId,
    string? BillingReason,
    DateTimeOffset? PeriodStart,
    string? OrganizationId) : BillingEvent(EventId, Created);

/// <summary>Collecting an invoice failed; the provider retries by its own schedule.</summary>
public sealed record InvoicePaymentFailed(
    string EventId,
    DateTimeOffset Created,
    string? CustomerId,
    string? SubscriptionId,
    string? OrganizationId) : BillingEvent(EventId, Created);

/// <summary>An event xChange does not act on.</summary>
public sealed record OtherBillingEvent(string EventId, DateTimeOffset Created, string Type) : BillingEvent(EventId, Created);
