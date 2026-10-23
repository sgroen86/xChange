// Sets up a Stripe account for xChange, idempotently. See README.md.
//
//   STRIPE_SECRET_KEY=sk_test_... dotnet run --project tools/stripe-setup -- <webhook-url> <secret-file>
//
// The webhook signing secret is written only to <secret-file>, never to the
// console. Running it again changes nothing that is already in place, and
// never overwrites a price: once created, prices are managed in Stripe.

using InvoicePlatform.Application.Billing;
using Stripe;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/stripe-setup -- <webhook-url> <secret-file>");
    Console.Error.WriteLine("  e.g. https://<function-url>/api/v1/stripe/webhook ./stripe-webhook-secret.txt");
    return 2;
}

var webhookUrl = args[0];
var secretFile = Path.GetFullPath(args[1]);

if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var parsedUrl) || parsedUrl.Scheme != Uri.UriSchemeHttps)
{
    Console.Error.WriteLine($"The webhook URL must be an absolute https URL: {webhookUrl}");
    return 2;
}

var secretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
if (string.IsNullOrWhiteSpace(secretKey))
{
    Console.Error.WriteLine("Set STRIPE_SECRET_KEY to the account's secret key (sk_test_... or sk_live_...).");
    return 2;
}

var client = new StripeClient(secretKey.Trim());
Console.WriteLine($"Stripe account mode: {(secretKey.Contains("_live_", StringComparison.Ordinal) ? "LIVE" : "test")}");
Console.WriteLine($"Stripe API version: {StripeConfiguration.ApiVersion}");

// The webhook secret can only be read when the endpoint is created. Refuse to
// start if it could not be written, rather than create an endpoint and lose it.
var webhooks = new WebhookEndpointService(client);
var existingWebhook = (await webhooks.ListAsync(new WebhookEndpointListOptions { Limit = 100 })).Data
    .FirstOrDefault(w => string.Equals(w.Url, webhookUrl, StringComparison.Ordinal));
if (existingWebhook is null && System.IO.File.Exists(secretFile))
{
    Console.Error.WriteLine($"{secretFile} already exists; give a path that does not, so the new signing secret is not mixed up with an old one.");
    return 2;
}

// ---- products and prices --------------------------------------------------
//
// Initial prices (EUR cents, excluding VAT). After this they live in Stripe:
// a new price there, with the same lookup key transferred to it, changes what
// xChange charges without a deploy.
var initialCents = new Dictionary<string, long>
{
    [BillingCatalogue.Starter.LookupKey] = 1900,
    [BillingCatalogue.Pro.LookupKey] = 4900,
    [BillingCatalogue.Business.LookupKey] = 12900,
    [BillingCatalogue.Credits.LookupKey] = 3000,
};

var products = new ProductService(client);
var prices = new PriceService(client);
var planPrices = new List<(string ProductId, string PriceId)>();

foreach (var plan in BillingCatalogue.Plans)
{
    var priceId = await EnsurePriceAsync(
        $"xchange_{plan.Id}",
        $"xChange {plan.Name}",
        $"{plan.MonthlyConversions} PDF conversions per month",
        plan.LookupKey,
        initialCents[plan.LookupKey],
        monthly: true);
    planPrices.Add(($"xchange_{plan.Id}", priceId));
}

await EnsurePriceAsync(
    "xchange_credits",
    $"xChange {BillingCatalogue.Credits.Credits} credits",
    $"{BillingCatalogue.Credits.Credits} PDF conversions, never expire",
    BillingCatalogue.Credits.LookupKey,
    initialCents[BillingCatalogue.Credits.LookupKey],
    monthly: false);

// ---- customer portal ------------------------------------------------------
//
// Found again by its metadata; the API looks it up the same way
// (StripeBillingProvider.PortalMetadataKey/Value).
var portals = new Stripe.BillingPortal.ConfigurationService(client);
var features = new Stripe.BillingPortal.ConfigurationFeaturesOptions
{
    InvoiceHistory = new Stripe.BillingPortal.ConfigurationFeaturesInvoiceHistoryOptions { Enabled = true },
    PaymentMethodUpdate = new Stripe.BillingPortal.ConfigurationFeaturesPaymentMethodUpdateOptions { Enabled = true },
    SubscriptionCancel = new Stripe.BillingPortal.ConfigurationFeaturesSubscriptionCancelOptions
    {
        Enabled = true,
        Mode = "at_period_end",
        ProrationBehavior = "none",
    },
    SubscriptionUpdate = new Stripe.BillingPortal.ConfigurationFeaturesSubscriptionUpdateOptions
    {
        Enabled = true,
        DefaultAllowedUpdates = ["price"],
        ProrationBehavior = "create_prorations",
        Products = [.. planPrices.Select(p => new Stripe.BillingPortal.ConfigurationFeaturesSubscriptionUpdateProductOptions
        {
            Product = p.ProductId,
            Prices = [p.PriceId],
        })],
    },
};

var existingPortal = (await portals.ListAsync(new Stripe.BillingPortal.ConfigurationListOptions { Active = true, Limit = 100 })).Data
    .FirstOrDefault(c => c.Metadata is not null && c.Metadata.TryGetValue("xchange", out var value) && value == "portal");
if (existingPortal is null)
{
    var created = await portals.CreateAsync(new Stripe.BillingPortal.ConfigurationCreateOptions
    {
        Name = "xChange",
        Features = features,
        Metadata = new Dictionary<string, string> { ["xchange"] = "portal" },
    });
    Console.WriteLine($"Portal configuration created: {created.Id}");
}
else
{
    await portals.UpdateAsync(existingPortal.Id, new Stripe.BillingPortal.ConfigurationUpdateOptions
    {
        Features = new Stripe.BillingPortal.ConfigurationFeaturesOptions
        {
            InvoiceHistory = features.InvoiceHistory,
            PaymentMethodUpdate = features.PaymentMethodUpdate,
            SubscriptionCancel = features.SubscriptionCancel,
            SubscriptionUpdate = features.SubscriptionUpdate,
        },
    });
    Console.WriteLine($"Portal configuration updated: {existingPortal.Id}");
}

// ---- webhook --------------------------------------------------------------
List<string> events =
[
    "checkout.session.completed",
    "checkout.session.async_payment_succeeded",
    "customer.subscription.created",
    "customer.subscription.updated",
    "customer.subscription.deleted",
    "invoice.paid",
    "invoice.payment_failed",
];

if (existingWebhook is null)
{
    var created = await webhooks.CreateAsync(new WebhookEndpointCreateOptions
    {
        Url = webhookUrl,
        EnabledEvents = events,

        // The library's version, so the events have the shape the API reads.
        ApiVersion = StripeConfiguration.ApiVersion,
        Description = "xChange API",
    });

    await using (var file = new FileStream(secretFile, FileMode.CreateNew, FileAccess.Write))
    await using (var writer = new StreamWriter(file))
    {
        await writer.WriteAsync(created.Secret);
    }

    Console.WriteLine($"Webhook endpoint created: {created.Id}");
    Console.WriteLine($"Its signing secret is in {secretFile} - put it in xchange/stripe as webhookSecret, then delete the file.");
}
else
{
    await webhooks.UpdateAsync(existingWebhook.Id, new WebhookEndpointUpdateOptions { EnabledEvents = events, Disabled = false });
    Console.WriteLine($"Webhook endpoint already existed, events updated: {existingWebhook.Id} (API version {existingWebhook.ApiVersion})");
    Console.WriteLine("Its signing secret cannot be read again through the API; reveal or roll it in the Stripe dashboard. Nothing was written.");
    if (existingWebhook.ApiVersion != StripeConfiguration.ApiVersion)
    {
        Console.WriteLine($"WARNING: the endpoint uses API version {existingWebhook.ApiVersion}, the API expects {StripeConfiguration.ApiVersion}. "
            + "Delete the endpoint in the dashboard and run this again to recreate it.");
    }
}

Console.WriteLine("Done.");
return 0;

async Task<string> EnsurePriceAsync(string productId, string name, string description, string lookupKey, long cents, bool monthly)
{
    var existing = (await prices.ListAsync(new PriceListOptions { LookupKeys = [lookupKey], Limit = 1 })).Data.FirstOrDefault();
    if (existing is not null)
    {
        Console.WriteLine($"Price {lookupKey} exists: {existing.Id}, {existing.UnitAmount / 100m:0.00} {existing.Currency?.ToUpperInvariant()}"
            + (existing.Active ? string.Empty : " (INACTIVE - activate it in the dashboard, or checkout will fail)"));
        return existing.Id;
    }

    Product product;
    try
    {
        product = await products.GetAsync(productId);
        Console.WriteLine($"Product {productId} exists");
    }
    catch (StripeException exception) when (exception.StripeError?.Code == "resource_missing")
    {
        product = await products.CreateAsync(new ProductCreateOptions
        {
            Id = productId,
            Name = name,
            Description = description,
            Metadata = new Dictionary<string, string> { ["xchange"] = "plan" },
        });
        Console.WriteLine($"Product {productId} created");
    }

    var price = await prices.CreateAsync(new PriceCreateOptions
    {
        Product = product.Id,
        Currency = "eur",
        UnitAmount = cents,
        TaxBehavior = "exclusive",
        LookupKey = lookupKey,
        Nickname = name,
        Recurring = monthly ? new PriceRecurringOptions { Interval = "month" } : null,
    });
    Console.WriteLine($"Price {lookupKey} created: {price.Id}, {cents / 100m:0.00} EUR");
    return price.Id;
}
