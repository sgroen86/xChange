using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// The billing endpoints with in-memory state and the real Stripe adapter,
/// which never reaches Stripe here: without a secret key the payment endpoints
/// say payments are unavailable, and webhooks are verified locally.
/// </summary>
public class BillingEndpointTests : IClassFixture<SignedInFactory>
{
    private const string WebhookSecret = "whsec_test_only_never_real";

    private readonly SignedInFactory _factory;

    public BillingEndpointTests(SignedInFactory factory) => _factory = factory;

    [Fact]
    public async Task Everyone_signed_in_sees_the_status_and_nobody_else()
    {
        await _factory.Usage.SetLimitAsync(SignedInFactory.OrgB, 10, trial: true);

        var anonymous = await _factory.CreateClient().GetAsync("/api/v1/billing");
        var status = await _factory.As("admin-b").GetFromJsonAsync<JsonElement>("/api/v1/billing");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.True(status.GetProperty("trial").GetBoolean());
        Assert.Equal(10, status.GetProperty("baseLimit").GetInt32());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("plan").ValueKind);
        Assert.False(status.GetProperty("hasCustomer").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await _factory.As("readonly-a").GetAsync("/api/v1/billing")).StatusCode);
    }

    [Theory]
    [InlineData("user-a")]
    [InlineData("readonly-a")]
    public async Task Only_an_admin_may_start_a_checkout_or_open_the_portal(string token)
    {
        var client = _factory.As(token);

        var checkout = await client.PostAsJsonAsync("/api/v1/billing/checkout", new { plan = "pro" });
        var portal = await client.PostAsync("/api/v1/billing/portal", null);

        Assert.Equal(HttpStatusCode.Forbidden, checkout.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, portal.StatusCode);
    }

    [Fact]
    public async Task Anonymous_callers_cannot_buy_or_see_plans()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/billing/checkout", new { plan = "pro" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/billing/portal", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/billing/plans")).StatusCode);
    }

    [Fact]
    public async Task Without_stripe_keys_an_admin_is_told_payments_are_unavailable()
    {
        var admin = _factory.As("admin-a");

        var checkout = await admin.PostAsJsonAsync("/api/v1/billing/checkout", new { plan = "pro" });
        var plans = await admin.GetAsync("/api/v1/billing/plans");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, checkout.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, plans.StatusCode);
    }

    [Fact]
    public async Task A_checkout_needs_a_plan_or_credits()
    {
        var response = await _factory.As("admin-a").PostAsJsonAsync("/api/v1/billing/checkout", new { plan = "platinum" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_portal_needs_a_customer()
    {
        var response = await _factory.As("admin-b").PostAsync("/api/v1/billing/portal", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Without_a_webhook_secret_the_webhook_answers_503_so_stripe_retries()
    {
        var payload = CreditsPaid("evt_unconfigured", "cs_unconfigured", "cus_unconfigured");

        var response = await Post(_factory.CreateClient(), payload, Sign(payload, WebhookSecret));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task A_webhook_with_a_bad_missing_or_stale_signature_is_refused()
    {
        using var configured = WithWebhookSecret();
        var client = configured.CreateClient();
        var payload = CreditsPaid("evt_forged", "cs_forged", "cus_forged");

        var wrongSecret = await Post(client, payload, Sign(payload, "whsec_someone_else"));
        var missing = await Post(client, payload, null);
        var stale = await Post(client, payload, Sign(payload, WebhookSecret, DateTimeOffset.UtcNow.AddMinutes(-10)));
        var tampered = await Post(client, payload.Replace("cus_forged", "cus_other"), Sign(payload, WebhookSecret));

        Assert.All(new[] { wrongSecret, missing, stale, tampered }, r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
    }

    [Fact]
    public async Task A_correctly_signed_credit_purchase_adds_credits_once()
    {
        using var configured = WithWebhookSecret();
        var client = configured.CreateClient();
        await _factory.Usage.LinkCustomerAsync(SignedInFactory.OrgA, "cus_paid_a");
        var payload = CreditsPaid("evt_paid_1", "cs_paid_1", "cus_paid_a");

        var first = await Post(client, payload, Sign(payload, WebhookSecret));
        var retry = await Post(client, payload, Sign(payload, WebhookSecret));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(20, _factory.Usage.For(SignedInFactory.OrgA).Credits);
    }

    [Fact]
    public async Task A_correctly_signed_subscription_sets_the_plan_from_its_lookup_key()
    {
        using var configured = WithWebhookSecret();
        await _factory.Usage.LinkCustomerAsync("org-sub", "cus_sub");
        var payload = SubscriptionUpdated("evt_sub_1", "cus_sub", "xchange_pro_monthly");

        var response = await Post(configured.CreateClient(), payload, Sign(payload, WebhookSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var org = _factory.Usage.For("org-sub");
        Assert.Equal(("pro", 250, "active"), (org.PlanId, org.PeriodAllowance, org.Status));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_761_955_200), org.PeriodStart);
    }

    [Fact]
    public async Task A_cancellation_scheduled_with_cancel_at_means_the_plan_ends_at_the_period_end()
    {
        // How the Customer Portal cancels today: cancel_at = period end, cancel_at_period_end = false.
        using var configured = WithWebhookSecret();
        await _factory.Usage.LinkCustomerAsync("org-sub", "cus_sub");
        var payload = SubscriptionUpdated("evt_sub_cancel", "cus_sub", "xchange_pro_monthly", cancelAt: "1764547200");

        var response = await Post(configured.CreateClient(), payload, Sign(payload, WebhookSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(_factory.Usage.For("org-sub").CancelAtPeriodEnd);
    }

    [Fact]
    public async Task An_event_xchange_does_not_handle_is_acknowledged()
    {
        using var configured = WithWebhookSecret();
        var payload = """{"id":"evt_other","object":"event","api_version":"2026-09-30.endive","created":1761955200,"type":"customer.created","livemode":false,"pending_webhooks":1,"data":{"object":{"id":"cus_x","object":"customer"}}}""";

        var response = await Post(configured.CreateClient(), payload, Sign(payload, WebhookSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private WebApplicationFactory<Program> WithWebhookSecret()
        => _factory.WithWebHostBuilder(builder => builder.UseSetting("Stripe:WebhookSecret", WebhookSecret));

    private static async Task<HttpResponseMessage> Post(HttpClient client, string payload, string? signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/stripe/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        if (signature is not null)
        {
            request.Headers.TryAddWithoutValidation("Stripe-Signature", signature);
        }

        return await client.SendAsync(request);
    }

    /// <summary>Stripe's scheme, done by hand: t=timestamp, v1=HMAC-SHA256(secret, "timestamp.payload") in hex.</summary>
    private static string Sign(string payload, string secret, DateTimeOffset? at = null)
    {
        var timestamp = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(mac)}";
    }

    private static string CreditsPaid(string eventId, string sessionId, string customerId) => $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2026-09-30.endive",
          "created": 1761955200,
          "type": "checkout.session.completed",
          "livemode": false,
          "pending_webhooks": 1,
          "data": {
            "object": {
              "id": "{{sessionId}}",
              "object": "checkout.session",
              "mode": "payment",
              "payment_status": "paid",
              "status": "complete",
              "customer": "{{customerId}}",
              "client_reference_id": "org-a",
              "metadata": { "organizationId": "org-a", "lookupKey": "xchange_credits_20" }
            }
          }
        }
        """;

    private static string SubscriptionUpdated(string eventId, string customerId, string lookupKey, string cancelAt = "null") => $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2026-09-30.endive",
          "created": 1761955200,
          "type": "customer.subscription.updated",
          "livemode": false,
          "pending_webhooks": 1,
          "data": {
            "object": {
              "id": "sub_test_1",
              "object": "subscription",
              "customer": "{{customerId}}",
              "status": "active",
              "cancel_at_period_end": false,
              "cancel_at": {{cancelAt}},
              "metadata": { "organizationId": "org-sub" },
              "items": {
                "object": "list",
                "has_more": false,
                "url": "/v1/subscription_items",
                "data": [
                  {
                    "id": "si_test_1",
                    "object": "subscription_item",
                    "current_period_start": 1761955200,
                    "current_period_end": 1764547200,
                    "price": { "id": "price_test_1", "object": "price", "lookup_key": "{{lookupKey}}", "unit_amount": 4900, "currency": "eur" }
                  }
                ]
              }
            }
          }
        }
        """;
}
