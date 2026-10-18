using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Api.Identity;

/// <summary>
/// Resolves the bearer token on every request and puts the user in
/// <see cref="HttpContext.Items"/>.
///
/// It only identifies; it never rejects. Enforcement lives in
/// <see cref="AuthorizationExtensions.RequireAuthenticatedUser"/> on the routes
/// that need it, so a route is protected by saying so rather than by being
/// forgotten about.
/// </summary>
public sealed class AuthenticationMiddleware(RequestDelegate next)
{
    internal const string UserKey = "xchange.user";
    internal const string ApiKeyCallerKey = "xchange.apikey";

    public async Task InvokeAsync(HttpContext context, AuthService auth)
    {
        var token = AuthEndpoints.ReadBearerToken(context);

        if (ApiKey.LooksLikeKey(token))
        {
            // An API key is not a user: it is kept apart, so every route that
            // asks for a user still refuses it. Only RequireApiKey accepts it.
            var keys = context.RequestServices.GetRequiredService<ApiKeyService>();
            var caller = await keys.AuthenticateAsync(token!, context.RequestAborted).ConfigureAwait(false);
            if (caller is not null)
            {
                context.Items[ApiKeyCallerKey] = caller;
            }
        }
        else if (token is not null)
        {
            var user = await auth.ValidateAsync(token, context.RequestAborted).ConfigureAwait(false);
            if (user is not null)
            {
                context.Items[UserKey] = user;
            }
        }

        await next(context).ConfigureAwait(false);
    }
}

public static class AuthorizationExtensions
{
    public static AuthenticatedUser? GetAuthenticatedUser(this HttpContext context)
        => context.Items.TryGetValue(AuthenticationMiddleware.UserKey, out var value)
            ? value as AuthenticatedUser
            : null;

    public static ApiKeyCaller? GetApiKeyCaller(this HttpContext context)
        => context.Items.TryGetValue(AuthenticationMiddleware.ApiKeyCallerKey, out var value)
            ? value as ApiKeyCaller
            : null;

    /// <summary>
    /// Rejects anyone without a valid API key with 401 - a signed-in user
    /// included: the conversion API is for systems, the app has its own routes.
    /// </summary>
    public static TBuilder RequireApiKey<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            if (context.HttpContext.GetApiKeyCaller() is null)
            {
                return Results.Problem(
                    detail: "Stuur een geldige API-sleutel mee: Authorization: Bearer xck_…",
                    title: "Geen geldige API-sleutel",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return await next(context).ConfigureAwait(false);
        });

        return builder;
    }

    /// <summary>
    /// Rejects anonymous callers with 401. Applied per route rather than
    /// globally so that login and setup stay reachable, and so protecting a new
    /// route is a visible decision.
    /// </summary>
    public static TBuilder RequireAuthenticatedUser<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            if (context.HttpContext.GetAuthenticatedUser() is null)
            {
                return Results.Problem(
                    detail: "Log in om deze actie uit te voeren.",
                    title: "Niet ingelogd",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return await next(context).ConfigureAwait(false);
        });

        return builder;
    }

    /// <summary>
    /// Rejects a signed-in user whose role cannot perform the action. Readonly
    /// accounts exist in Bookkeeping's role set, so they must be refused
    /// somewhere rather than quietly granted write access.
    /// </summary>
    public static TBuilder RequireRole<TBuilder>(this TBuilder builder, params UserRoleName[] allowed)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var user = context.HttpContext.GetAuthenticatedUser();

            if (user is null)
            {
                return Results.Problem(
                    detail: "Log in om deze actie uit te voeren.",
                    title: "Niet ingelogd",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (!allowed.Any(role => role.ToString() == user.Role.ToString()))
            {
                return Results.Problem(
                    detail: "Uw account heeft hiervoor geen rechten.",
                    title: "Geen toegang",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return await next(context).ConfigureAwait(false);
        });

        return builder;
    }

    /// <summary>
    /// Rejects anyone who is not a platform administrator - including the Admin
    /// of an organisation, whose role says nothing about the platform.
    /// </summary>
    public static TBuilder RequirePlatformAdmin<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var user = context.HttpContext.GetAuthenticatedUser();

            if (user is null)
            {
                return Results.Problem(
                    detail: "Log in om deze actie uit te voeren.",
                    title: "Niet ingelogd",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (!user.IsPlatformAdmin)
            {
                return Results.Problem(
                    detail: "Alleen de platformbeheerder kan dit.",
                    title: "Geen toegang",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return await next(context).ConfigureAwait(false);
        });

        return builder;
    }
}

/// <summary>Mirrors Domain's UserRole without the API referencing it by value.</summary>
public enum UserRoleName
{
    Admin,
    User,
    ReadOnly,
}
