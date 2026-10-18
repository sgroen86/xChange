using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Contracts.Conversions;
using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Api.Conversions;

/// <summary>
/// The organisation's API keys, managed by an admin. The organisation comes
/// from the session; a key is shown once, when it is made.
/// </summary>
public static class ApiKeyEndpoints
{
    public static RouteGroupBuilder MapApiKeyEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs). A key acts
        // for the whole organisation, so only an admin may see, make or revoke one.
        group.MapGet("/organization/api-keys", ListAsync)
            .WithName("ListApiKeys")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin);

        group.MapPost("/organization/api-keys", CreateAsync)
            .WithName("CreateApiKey")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin);

        group.MapDelete("/organization/api-keys/{keyId}", RevokeAsync)
            .WithName("RevokeApiKey")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin);

        return group;
    }

    private static async Task<IResult> ListAsync(HttpContext context, ApiKeyService keys, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var list = await keys.ListAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(list.Select(ToResponse));
    }

    private static async Task<IResult> CreateAsync(HttpContext context, ApiKeyService keys, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;

        // Read here, not bound as a parameter: binding runs before the guards.
        var request = await RequestBody.ReadAsync<CreateApiKeyRequest>(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var created = await keys.CreateAsync(user.OrganizationId, user.Id, request.Name ?? "", cancellationToken).ConfigureAwait(false);
        if (created is null)
        {
            return Results.Problem(
                detail: $"Geef de sleutel een naam van hoogstens {ApiKeyService.MaxNameLength} tekens.",
                title: "Sleutel niet aangemaakt",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var key = ToResponse(created.Key);
        return Results.Created($"/api/v1/organization/api-keys/{created.Key.KeyId}", new CreatedApiKeyResponse
        {
            KeyId = key.KeyId,
            Name = key.Name,
            Prefix = key.Prefix,
            CreatedAt = key.CreatedAt,
            LastUsedAt = key.LastUsedAt,
            Key = created.Secret,
        });
    }

    private static async Task<IResult> RevokeAsync(string keyId, HttpContext context, ApiKeyService keys, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        return await keys.RevokeAsync(user.OrganizationId, keyId, cancellationToken).ConfigureAwait(false)
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static ApiKeyResponse ToResponse(ApiKey key) => new()
    {
        KeyId = key.KeyId,
        Name = key.Name,
        Prefix = key.VisiblePrefix,
        CreatedAt = key.CreatedAt,
        LastUsedAt = key.LastUsedAt,
    };
}
