using InvoicePlatform.Application.Identity;

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

    public async Task InvokeAsync(HttpContext context, AuthService auth)
    {
        var token = AuthEndpoints.ReadBearerToken(context);

        if (token is not null)
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
}

/// <summary>Mirrors Domain's UserRole without the API referencing it by value.</summary>
public enum UserRoleName
{
    Admin,
    User,
    ReadOnly,
}
