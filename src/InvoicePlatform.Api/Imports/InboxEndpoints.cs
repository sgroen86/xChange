using System.Text.Json.Serialization;
using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Imports;

namespace InvoicePlatform.Api.Imports;

/// <summary>
/// The organisation's inbox address. The organisation comes from the session;
/// only an admin may replace the address, because the old one stops working.
/// </summary>
public static class InboxEndpoints
{
    public sealed record InboxResponse([property: JsonPropertyName("address")] string? Address);

    public static RouteGroupBuilder MapInboxEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs).
        group.MapGet("/organization/inbox", GetAsync)
            .WithName("GetInboxAddress")
            .RequireAuthenticatedUser();

        group.MapPost("/organization/inbox", CreateAsync)
            .WithName("CreateInboxAddress")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin);

        return group;
    }

    private static async Task<IResult> GetAsync(HttpContext context, InboxService inbox, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        return Results.Ok(new InboxResponse(await inbox.GetAddressAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false)));
    }

    private static async Task<IResult> CreateAsync(HttpContext context, InboxService inbox, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        return Results.Ok(new InboxResponse(await inbox.CreateAddressAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false)));
    }
}
