using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Organizations;
using InvoicePlatform.Domain.Relations;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// Deleting an organisation: only the platform administrator, never their own
/// organisation, and when it happens the organisation's invoices, documents and
/// users are gone while every other organisation keeps its own.
/// </summary>
public class OrganizationDeletionEndpointTests : IClassFixture<SignedInFactory>
{
    private const string Password = "a-long-enough-password";

    private readonly SignedInFactory _factory;

    public OrganizationDeletionEndpointTests(SignedInFactory factory) => _factory = factory;

    private static HttpRequestMessage Delete(string organizationId, string? confirmName) => new(HttpMethod.Delete, $"/api/v1/platform/organizations/{organizationId}")
    {
        Content = JsonContent.Create(new { confirmName }),
    };

    private async Task SaveOrganizationAsync(string organizationId, string name)
        => await _factory.Platform.SaveOrganizationAsync(new Organization
        {
            OrganizationId = organizationId,
            Name = name,
            CountryCode = "NL",
            CreatedAt = DateTimeOffset.UnixEpoch,
        });

    private async Task<string> LogInAsync(string email)
    {
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Only_the_platform_administrator_may_delete_an_organisation()
    {
        await SaveOrganizationAsync(SignedInFactory.OrgB, "Org B");
        var invoice = await _factory.SeedInvoiceAsync(SignedInFactory.OrgB, new CanonicalInvoiceDraft());

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().SendAsync(Delete(SignedInFactory.OrgB, "Org B"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _factory.As("admin-a").SendAsync(Delete(SignedInFactory.OrgB, "Org B"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _factory.As("user-a").SendAsync(Delete(SignedInFactory.OrgB, "Org B"))).StatusCode);
        // Not even the organisation's own Admin.
        Assert.Equal(HttpStatusCode.Forbidden, (await _factory.As("admin-b").SendAsync(Delete(SignedInFactory.OrgB, "Org B"))).StatusCode);

        Assert.NotNull(await _factory.Invoices.GetAsync(SignedInFactory.OrgB, invoice));
        Assert.NotNull(await _factory.Platform.FindOrganizationAsync(SignedInFactory.OrgB));
    }

    [Fact]
    public async Task The_platform_administrator_deletes_an_organisation_and_only_that_one()
    {
        const string doomed = "org-doomed";
        await SaveOrganizationAsync(doomed, "Doomed BV");
        await _factory.AddUserAsync("piet@doomed", Password, UserRole.User, doomed);
        await _factory.AddUserAsync("klaas@doomed", Password, UserRole.User, doomed);
        var token = await LogInAsync("piet@doomed");
        Assert.Equal(HttpStatusCode.OK, (await _factory.As(token).GetAsync("/api/v1/auth/me")).StatusCode);

        var doomedInvoice = await _factory.SeedInvoiceAsync(doomed, new CanonicalInvoiceDraft());
        var doomedDocument = await _factory.Invoices.PutAsync(doomed, doomedInvoice, "%PDF-1.4"u8.ToArray(), "application/pdf");
        await _factory.Relations.SaveAsync(new Relation { RelationId = "r1", OrganizationId = doomed, Name = "Klant", UpdatedAt = DateTimeOffset.UnixEpoch });
        await _factory.Inboxes.AssignAsync(doomed, "doomed-inbox", null);

        var keptInvoice = await _factory.SeedInvoiceAsync(SignedInFactory.OrgB, new CanonicalInvoiceDraft());
        var keptDocument = await _factory.Invoices.PutAsync(SignedInFactory.OrgB, keptInvoice, "%PDF-1.4"u8.ToArray(), "application/pdf");

        var response = await _factory.As("platform-a").SendAsync(Delete(doomed, " Doomed BV "));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // The organisation, its data and its users are gone ...
        Assert.Null(await _factory.Platform.FindOrganizationAsync(doomed));
        Assert.Empty(await _factory.Invoices.ListAsync(doomed));
        Assert.Null(await _factory.Invoices.GetAsync(doomedDocument));
        Assert.Empty(await _factory.Relations.ListAsync(doomed));
        Assert.Null(await _factory.Inboxes.FindOrganizationAsync("doomed-inbox"));
        Assert.Null(await _factory.Identity.FindByEmailAsync("piet@doomed"));
        Assert.Null(await _factory.Identity.FindByEmailAsync("klaas@doomed"));
        // ... and a session from before stops working at once.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.As(token).GetAsync("/api/v1/auth/me")).StatusCode);

        var list = await _factory.As("platform-a").GetFromJsonAsync<JsonElement>("/api/v1/platform/organizations");
        Assert.DoesNotContain(list.EnumerateArray(), o => o.GetProperty("organizationId").GetString() == doomed);

        // Another organisation keeps everything.
        Assert.NotNull(await _factory.Invoices.GetAsync(SignedInFactory.OrgB, keptInvoice));
        Assert.NotNull(await _factory.Invoices.GetAsync(keptDocument));
        Assert.Equal(HttpStatusCode.OK, (await _factory.As("admin-b").GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.As("admin-b").GetAsync($"/api/v1/invoices/{keptInvoice}")).StatusCode);
    }

    [Fact]
    public async Task The_platform_administrators_own_organisation_is_refused()
    {
        await SaveOrganizationAsync(SignedInFactory.OrgA, "Org A");
        var invoice = await _factory.SeedInvoiceAsync(SignedInFactory.OrgA, new CanonicalInvoiceDraft());

        var response = await _factory.As("platform-a").SendAsync(Delete(SignedInFactory.OrgA, "Org A"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("detail").GetString()));
        Assert.NotNull(await _factory.Platform.FindOrganizationAsync(SignedInFactory.OrgA));
        Assert.NotNull(await _factory.Invoices.GetAsync(SignedInFactory.OrgA, invoice));
        Assert.Equal(HttpStatusCode.OK, (await _factory.As("admin-a").GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task A_running_subscription_is_refused_in_the_callers_language()
    {
        const string subscribed = "org-subscribed";
        await SaveOrganizationAsync(subscribed, "Subscribed BV");
        _factory.Usage.For(subscribed).Status = "active";

        var request = Delete(subscribed, "Subscribed BV");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en");
        var response = await _factory.As("platform-a").SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Not deleted", problem.GetProperty("title").GetString());
        Assert.Contains("Cancel it first", problem.GetProperty("detail").GetString());
        Assert.NotNull(await _factory.Platform.FindOrganizationAsync(subscribed));
    }

    [Fact]
    public async Task A_wrong_confirmation_is_a_bad_request_and_an_unknown_organisation_is_not_found()
    {
        const string kept = "org-kept";
        await SaveOrganizationAsync(kept, "Kept BV");

        Assert.Equal(HttpStatusCode.BadRequest, (await _factory.As("platform-a").SendAsync(Delete(kept, "kept bv"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _factory.As("platform-a").SendAsync(Delete(kept, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.As("platform-a").SendAsync(Delete("org-unknown", "x"))).StatusCode);
        Assert.NotNull(await _factory.Platform.FindOrganizationAsync(kept));
    }
}
