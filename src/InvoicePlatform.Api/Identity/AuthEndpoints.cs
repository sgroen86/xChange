using InvoicePlatform.Application.Identity;

namespace InvoicePlatform.Api.Identity;

/// <summary>
/// HTTP surface for login. Mirrors Bookkeeping's flow: set up the first account
/// when none exists, then log in, and everything else needs the session.
/// </summary>
public static class AuthEndpoints
{
    public const string BearerPrefix = "Bearer ";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/auth/status", StatusAsync)
            .WithName("AuthStatus")
            .WithSummary("Whether any account exists yet, which decides setup vs login.");

        group.MapPost("/auth/setup", SetupAsync)
            .WithName("AuthSetup")
            .WithSummary("Create the first account. Refused once one exists.");

        group.MapPost("/auth/login", LoginAsync)
            .WithName("AuthLogin");

        group.MapPost("/auth/logout", LogoutAsync)
            .WithName("AuthLogout");

        group.MapGet("/auth/me", MeAsync)
            .WithName("AuthMe");

        return group;
    }

    private static async Task<IResult> StatusAsync(IUserStore users, CancellationToken cancellationToken)
    {
        var exists = await users.AnyUsersExistAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(new { setupRequired = !exists });
    }

    private static async Task<IResult> SetupAsync(
        SetupRequest request,
        AuthService auth,
        CancellationToken cancellationToken)
    {
        var result = await auth.SetupFirstUserAsync(
            request.Email ?? string.Empty,
            request.Password ?? string.Empty,
            request.Name ?? string.Empty,
            request.OrganizationName ?? string.Empty,
            cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? Results.Ok(ToResponse(result))
            : Results.Problem(detail: result.Error, title: "Setup mislukt", statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext context,
        AuthService auth,
        CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(
            request.Email ?? string.Empty,
            request.Password ?? string.Empty,
            ClientKey(context),
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            // 429 for the lockout so a client can tell "wait" from "wrong",
            // without the message itself revealing which account exists.
            var status = result.Error is not null && result.Error.StartsWith("Te veel", StringComparison.Ordinal)
                ? StatusCodes.Status429TooManyRequests
                : StatusCodes.Status401Unauthorized;

            return Results.Problem(detail: result.Error, title: "Inloggen mislukt", statusCode: status);
        }

        return Results.Ok(ToResponse(result));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        AuthService auth,
        CancellationToken cancellationToken)
    {
        var token = ReadBearerToken(context);
        if (token is not null)
        {
            await auth.LogoutAsync(token, cancellationToken).ConfigureAwait(false);
        }

        // Always 204: whether the token was valid is not the caller's business,
        // and logging out twice is not an error.
        return Results.NoContent();
    }

    private static IResult MeAsync(HttpContext context)
    {
        var user = context.GetAuthenticatedUser();
        return user is null
            ? Results.Unauthorized()
            : Results.Ok(new
            {
                id = user.Id,
                organizationId = user.OrganizationId,
                email = user.Email,
                name = user.Name,
                role = user.Role.ToString(),
            });
    }

    /// <summary>
    /// The address used for rate limiting. Behind a Lambda Function URL the
    /// caller's address arrives in X-Forwarded-For; RemoteIpAddress is the
    /// proxy. Only the first entry is trusted - the rest are client-supplied.
    /// </summary>
    internal static string ClientKey(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    internal static string? ReadBearerToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.FirstOrDefault();
        return header is not null && header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? header[BearerPrefix.Length..].Trim()
            : null;
    }

    private static object ToResponse(AuthResult result) => new
    {
        token = result.Token,
        expiresAt = result.ExpiresAt,
        user = result.User is null
            ? null
            : new
            {
                id = result.User.Id,
                organizationId = result.User.OrganizationId,
                email = result.User.Email,
                name = result.User.Name,
                role = result.User.Role.ToString(),
            },
    };

    public sealed record SetupRequest(string? Email, string? Password, string? Name, string? OrganizationName);

    public sealed record LoginRequest(string? Email, string? Password);
}
