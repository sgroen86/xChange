using InvoicePlatform.Application.Billing;
using Microsoft.Extensions.Logging;
using Stripe;

namespace InvoicePlatform.Infrastructure.Billing;

/// <summary>
/// Stripe behind <see cref="IBillingProvider"/>. Every Stripe type stays in
/// this class: the rest of xChange sees lookup keys, URLs and its own events.
///
/// Prices are found by lookup_key and kept for a few minutes, so changing a
/// price in Stripe needs no deploy.
/// </summary>
internal sealed class StripeBillingProvider(StripeSecrets secrets, ILogger<StripeBillingProvider> logger) : IBillingProvider
{
    /// <summary>Written by tools/stripe-setup on the portal configuration it creates.</summary>
    public const string PortalMetadataKey = "xchange";

    public const string PortalMetadataValue = "portal";

    /// <summary>Stripe's default: a signature older than five minutes is refused (replay protection).</summary>
    private const long SignatureToleranceSeconds = 300;

    private static readonly TimeSpan PriceLifetime = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private (DateTimeOffset ReadAt, IReadOnlyDictionary<string, (string PriceId, BillingPrice Price)> Prices)? _prices;
    private string? _portalConfigurationId;
    private (string Key, StripeClient Client)? _client;

    public async Task<string> CreateCustomerAsync(string organizationId, string organizationName, string email, CancellationToken cancellationToken = default)
    {
        var client = await ClientAsync(cancellationToken).ConfigureAwait(false);
        var customer = await CallAsync(() => new CustomerService(client).CreateAsync(
            new CustomerCreateOptions
            {
                Name = organizationName,
                Email = email,
                Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId },
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return customer.Id;
    }

    public async Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        var client = await ClientAsync(cancellationToken).ConfigureAwait(false);
        var prices = await PricesAsync(client, cancellationToken).ConfigureAwait(false);
        if (!prices.TryGetValue(request.LookupKey, out var price))
        {
            throw new BillingUnavailableException($"Stripe has no active price with lookup key {request.LookupKey}.");
        }

        // The organisation travels on the session and, for a plan, on the
        // subscription: the webhooks find it there if the customer link is missing.
        var metadata = new Dictionary<string, string>
        {
            ["organizationId"] = request.OrganizationId,
            ["lookupKey"] = request.LookupKey,
        };

        var subscription = request.Mode == CheckoutMode.Subscription;
        var session = await CallAsync(() => new Stripe.Checkout.SessionService(client).CreateAsync(
            new Stripe.Checkout.SessionCreateOptions
            {
                Mode = subscription ? "subscription" : "payment",
                Customer = request.CustomerId,
                ClientReferenceId = request.OrganizationId,
                Metadata = metadata,
                LineItems = [new Stripe.Checkout.SessionLineItemOptions { Price = price.PriceId, Quantity = 1 }],
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                SubscriptionData = subscription
                    ? new Stripe.Checkout.SessionSubscriptionDataOptions { Metadata = new Dictionary<string, string>(metadata) }
                    : null,
                PaymentIntentData = subscription
                    ? null
                    : new Stripe.Checkout.SessionPaymentIntentDataOptions { Metadata = new Dictionary<string, string>(metadata) },
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return session.Url;
    }

    public async Task<string> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken = default)
    {
        var client = await ClientAsync(cancellationToken).ConfigureAwait(false);
        var configuration = await PortalConfigurationAsync(client, cancellationToken).ConfigureAwait(false);
        var session = await CallAsync(() => new Stripe.BillingPortal.SessionService(client).CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = customerId,
                ReturnUrl = returnUrl,
                Configuration = configuration,
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return session.Url;
    }

    public async Task<IReadOnlyList<BillingPrice>> GetPricesAsync(CancellationToken cancellationToken = default)
    {
        var client = await ClientAsync(cancellationToken).ConfigureAwait(false);
        var prices = await PricesAsync(client, cancellationToken).ConfigureAwait(false);
        return [.. BillingCatalogue.LookupKeys.Where(prices.ContainsKey).Select(key => prices[key].Price)];
    }

    public async Task<WebhookParseResult> ParseWebhookAsync(string payload, string? signatureHeader, CancellationToken cancellationToken = default)
    {
        var keys = await secrets.GetAsync(cancellationToken).ConfigureAwait(false);
        if (keys.WebhookSecret is null)
        {
            return WebhookParseResult.NotConfigured;
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return WebhookParseResult.InvalidSignature;
        }

        Event stripeEvent;
        try
        {
            EventUtility.ValidateSignature(payload, signatureHeader, keys.WebhookSecret, SignatureToleranceSeconds);

            // The endpoint is created with this library's API version
            // (tools/stripe-setup). A mismatch is logged, not refused: refusing
            // would only make Stripe retry the same event for three days.
            stripeEvent = EventUtility.ParseEvent(payload, throwOnApiVersionMismatch: false);
        }
        catch (Exception exception) when (exception is StripeException or Newtonsoft.Json.JsonException or ArgumentException)
        {
            logger.LogWarning("A Stripe webhook was refused: {Reason}", exception.Message);
            return WebhookParseResult.InvalidSignature;
        }

        if (stripeEvent.ApiVersion is { } version && version != StripeConfiguration.ApiVersion)
        {
            logger.LogWarning(
                "Stripe event {EventId} has API version {Version}; this build expects {Expected}",
                stripeEvent.Id, version, StripeConfiguration.ApiVersion);
        }

        return new WebhookParseResult(WebhookParseStatus.Parsed, Translate(stripeEvent));
    }

    /// <summary>Stripe's event as one of ours; an event of a known type with an unexpected object is "other".</summary>
    private static BillingEvent Translate(Event e)
    {
        var created = new DateTimeOffset(DateTime.SpecifyKind(e.Created, DateTimeKind.Utc));

        switch (e.Type)
        {
            case "checkout.session.completed":
            case "checkout.session.async_payment_succeeded":
                if (e.Data.Object is Stripe.Checkout.Session session)
                {
                    return new CheckoutCompleted(
                        e.Id,
                        created,
                        session.Id,
                        session.CustomerId,
                        Blank(session.ClientReferenceId) ?? Meta(session.Metadata, "organizationId"),
                        OneOffPayment: session.Mode == "payment",
                        Paid: session.PaymentStatus == "paid",
                        LookupKey: Meta(session.Metadata, "lookupKey"));
                }

                break;

            case "customer.subscription.created":
            case "customer.subscription.updated":
            case "customer.subscription.deleted":
                if (e.Data.Object is Subscription subscription)
                {
                    // Since API version 2025-03-31 the period lives on the item.
                    var item = subscription.Items?.Data?.FirstOrDefault();
                    return new SubscriptionChanged(
                        e.Id,
                        created,
                        subscription.CustomerId,
                        subscription.Id,
                        subscription.Status,
                        item?.Price?.LookupKey,
                        Utc(item?.CurrentPeriodStart),
                        Utc(item?.CurrentPeriodEnd),
                        subscription.CancelAtPeriodEnd,
                        Meta(subscription.Metadata, "organizationId"));
                }

                break;

            case "invoice.paid":
                if (e.Data.Object is Invoice paid)
                {
                    var details = paid.Parent?.SubscriptionDetails;
                    return new InvoicePaid(
                        e.Id,
                        created,
                        paid.CustomerId,
                        details?.SubscriptionId,
                        paid.BillingReason,
                        Utc(paid.Lines?.Data?.FirstOrDefault()?.Period?.Start),
                        Meta(details?.Metadata, "organizationId"));
                }

                break;

            case "invoice.payment_failed":
                if (e.Data.Object is Invoice failed)
                {
                    var details = failed.Parent?.SubscriptionDetails;
                    return new InvoicePaymentFailed(
                        e.Id,
                        created,
                        failed.CustomerId,
                        details?.SubscriptionId,
                        Meta(details?.Metadata, "organizationId"));
                }

                break;
        }

        return new OtherBillingEvent(e.Id, created, e.Type);
    }

    private async Task<StripeClient> ClientAsync(CancellationToken cancellationToken)
    {
        var keys = await secrets.GetAsync(cancellationToken).ConfigureAwait(false);
        if (keys.SecretKey is not { } secretKey)
        {
            throw new BillingUnavailableException("No Stripe secret key is configured.");
        }

        // One client (and so one HTTP connection pool) per key.
        if (_client is { } current && current.Key == secretKey)
        {
            return current.Client;
        }

        var client = new StripeClient(secretKey);
        _client = (secretKey, client);
        return client;
    }

    private async Task<IReadOnlyDictionary<string, (string PriceId, BillingPrice Price)>> PricesAsync(
        StripeClient client,
        CancellationToken cancellationToken)
    {
        if (_prices is { } cached && DateTimeOffset.UtcNow - cached.ReadAt < PriceLifetime)
        {
            return cached.Prices;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_prices is { } again && DateTimeOffset.UtcNow - again.ReadAt < PriceLifetime)
            {
                return again.Prices;
            }

            var list = await CallAsync(() => new PriceService(client).ListAsync(
                new PriceListOptions { LookupKeys = [.. BillingCatalogue.LookupKeys], Active = true, Limit = 100 },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            var prices = new Dictionary<string, (string, BillingPrice)>();
            foreach (var price in list.Data)
            {
                if (price.LookupKey is { } key && price.UnitAmount is { } cents)
                {
                    // Stripe counts in the currency's minor unit; EUR has two decimals, kept as "19.00".
                    prices[key] = (price.Id, new BillingPrice(key, cents * 0.01m, price.Currency.ToUpperInvariant()));
                }
            }

            _prices = (DateTimeOffset.UtcNow, prices);
            return prices;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The configuration tools/stripe-setup made; without one, Stripe's default.</summary>
    private async Task<string?> PortalConfigurationAsync(StripeClient client, CancellationToken cancellationToken)
    {
        if (_portalConfigurationId is not null)
        {
            return _portalConfigurationId;
        }

        var configurations = await CallAsync(() => new Stripe.BillingPortal.ConfigurationService(client).ListAsync(
            new Stripe.BillingPortal.ConfigurationListOptions { Active = true, Limit = 100 },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return _portalConfigurationId = configurations.Data
            .FirstOrDefault(c => Meta(c.Metadata, PortalMetadataKey) == PortalMetadataValue)?.Id;
    }

    /// <summary>A Stripe failure, as the one exception the application knows.</summary>
    private static async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (StripeException exception)
        {
            throw new BillingUnavailableException($"Stripe refused the request: {exception.Message}", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new BillingUnavailableException("Stripe could not be reached.", exception);
        }
    }

    private static string? Meta(IDictionary<string, string>? metadata, string key)
        => metadata is not null && metadata.TryGetValue(key, out var value) ? Blank(value) : null;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static DateTimeOffset? Utc(DateTime? value)
        => value is { } v ? new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)) : null;
}
