using System.Text.Json.Serialization;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Usage;

namespace InvoicePlatform.Api.Identity;

/// <summary>
/// Self sign-up and the organisation's usage. Sign-up is public by necessity -
/// the person has no account yet - and counts towards the same per-address
/// lockout as login. It is refused while the platform administrator has not
/// opened it.
/// </summary>
public static class SignupEndpoints
{
    public sealed record SignupRequest(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("password")] string? Password,
        [property: JsonPropertyName("organizationName")] string? OrganizationName,
        [property: JsonPropertyName("countryCode")] string? CountryCode);

    public sealed record ConfirmRequest([property: JsonPropertyName("code")] string? Code);

    public static RouteGroupBuilder MapSignupEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/signup/status", StatusAsync)
            .WithName("SignupStatus");

        group.MapPost("/signup", StartAsync)
            .WithName("Signup");

        group.MapPost("/signup/confirm", ConfirmAsync)
            .WithName("ConfirmSignup");

        // Guards per endpoint, never on the group (see Program.cs).
        group.MapGet("/organization/usage", UsageAsync)
            .WithName("GetUsage")
            .RequireAuthenticatedUser();

        return group;
    }

    private static async Task<IResult> StatusAsync(SignupService signup, CancellationToken cancellationToken)
        => Results.Ok(new { open = await signup.IsOpenAsync(cancellationToken).ConfigureAwait(false), trialConversions = SignupService.TrialConversions });

    private static async Task<IResult> StartAsync(SignupRequest request, HttpContext context, SignupService signup, CancellationToken cancellationToken)
    {
        var result = await signup.StartAsync(
            request.Name ?? string.Empty,
            request.Email ?? string.Empty,
            request.Password ?? string.Empty,
            request.OrganizationName ?? string.Empty,
            request.CountryCode ?? string.Empty,
            AuthEndpoints.ClientKey(context),
            cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            // The same answer for a new and a known address: the mail differs, the response does not.
            SignupOutcome.MailSent => Results.Accepted(),
            SignupOutcome.Closed => Results.Problem(detail: result.Error, title: IdentityApiText.Current.SignupClosed, statusCode: StatusCodes.Status403Forbidden),
            SignupOutcome.Locked => Results.Problem(detail: result.Error, title: IdentityApiText.Current.TooManyAttempts, statusCode: StatusCodes.Status429TooManyRequests),
            _ => Results.Problem(detail: result.Error, title: IdentityApiText.Current.SignupNotSent, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    private static async Task<IResult> ConfirmAsync(ConfirmRequest request, HttpContext context, SignupService signup, CancellationToken cancellationToken)
    {
        var result = await signup.ConfirmAsync(request.Code ?? string.Empty, AuthEndpoints.ClientKey(context), cancellationToken).ConfigureAwait(false);
        return result.Succeeded
            ? Results.Ok(AuthEndpoints.ToResponse(result))
            : Results.Problem(detail: result.Error, title: IdentityApiText.Current.AccountNotCreated, statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> UsageAsync(HttpContext context, IUsageMeter usage, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var summary = await usage.GetAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new { used = summary.Used, limit = summary.Limit, remaining = summary.Remaining, trial = summary.Trial });
    }
}
