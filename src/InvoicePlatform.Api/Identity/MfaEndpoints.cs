using System.Text.Json.Serialization;
using InvoicePlatform.Application.Identity;

namespace InvoicePlatform.Api.Identity;

/// <summary>
/// Two-step verification over HTTP. The login routes are public by necessity -
/// there is no session yet, only the challenge the password step returned -
/// and count wrong codes towards the same per-address lockout as passwords.
/// The account routes need a session; the reset needs the platform administrator.
/// </summary>
public static class MfaEndpoints
{
    public sealed record VerifyRequest(
        [property: JsonPropertyName("challenge")] string? Challenge,
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("recoveryCode")] string? RecoveryCode);

    public sealed record ChallengeRequest([property: JsonPropertyName("challenge")] string? Challenge);

    public sealed record CodeRequest([property: JsonPropertyName("code")] string? Code);

    public sealed record ResetRequest([property: JsonPropertyName("email")] string? Email);

    public static RouteGroupBuilder MapMfaEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/login/mfa", VerifyLoginAsync)
            .WithName("AuthLoginMfa");

        group.MapPost("/auth/mfa/setup", StartLoginSetupAsync)
            .WithName("AuthMfaSetup");

        group.MapPost("/auth/mfa/setup/confirm", ConfirmLoginSetupAsync)
            .WithName("AuthMfaSetupConfirm");

        // Guards per endpoint, never on the group (see Program.cs).
        group.MapGet("/account/mfa", StatusAsync)
            .WithName("GetAccountMfa")
            .RequireAuthenticatedUser();

        group.MapPost("/account/mfa/setup", StartAccountSetupAsync)
            .WithName("StartAccountMfa")
            .RequireAuthenticatedUser();

        group.MapPost("/account/mfa/confirm", ConfirmAccountSetupAsync)
            .WithName("ConfirmAccountMfa")
            .RequireAuthenticatedUser();

        group.MapPost("/account/mfa/disable", DisableAsync)
            .WithName("DisableAccountMfa")
            .RequireAuthenticatedUser();

        group.MapPost("/account/mfa/recovery-codes", RegenerateAsync)
            .WithName("RegenerateRecoveryCodes")
            .RequireAuthenticatedUser();

        group.MapPost("/platform/users/mfa/reset", ResetAsync)
            .WithName("ResetUserMfa")
            .RequirePlatformAdmin();

        return group;
    }

    // ── During login ─────────────────────────────────────────────────────────

    private static async Task<IResult> VerifyLoginAsync(VerifyRequest request, HttpContext context, MfaService mfa, CancellationToken cancellationToken)
    {
        var result = await mfa.VerifyLoginAsync(
            request.Challenge ?? string.Empty, request.Code, request.RecoveryCode, AuthEndpoints.ClientKey(context), cancellationToken).ConfigureAwait(false);
        return ToLoginResult(result);
    }

    private static async Task<IResult> StartLoginSetupAsync(ChallengeRequest request, HttpContext context, MfaService mfa, CancellationToken cancellationToken)
    {
        var result = await mfa.StartLoginSetupAsync(request.Challenge ?? string.Empty, AuthEndpoints.ClientKey(context), cancellationToken)
            .ConfigureAwait(false);
        return result.Succeeded
            ? Results.Ok(ToSetupResponse(result.Setup!))
            : Problem(result, IdentityApiText.Current.VerificationFailed);
    }

    private static async Task<IResult> ConfirmLoginSetupAsync(VerifyRequest request, HttpContext context, MfaService mfa, CancellationToken cancellationToken)
    {
        var result = await mfa.ConfirmLoginSetupAsync(request.Challenge ?? string.Empty, request.Code, AuthEndpoints.ClientKey(context), cancellationToken)
            .ConfigureAwait(false);
        return ToLoginResult(result);
    }

    /// <summary>401 when the login must start again, 429 for the lockout, 400 for a wrong code that may be retried.</summary>
    private static IResult ToLoginResult(AuthResult result)
    {
        if (result.Succeeded)
        {
            return Results.Ok(AuthEndpoints.ToResponse(result));
        }

        var status = result.LockedOut
            ? StatusCodes.Status429TooManyRequests
            : result.ChallengeGone
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status400BadRequest;

        return Results.Problem(detail: result.Error, title: IdentityApiText.Current.VerificationFailed, statusCode: status);
    }

    // ── Signed in ────────────────────────────────────────────────────────────

    private static async Task<IResult> StatusAsync(HttpContext context, MfaService mfa, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var status = await mfa.GetStatusAsync(user.OrganizationId, user.Id, cancellationToken).ConfigureAwait(false);
        return status is null
            ? Results.NotFound()
            : Results.Ok(new { enabled = status.Enabled, mandatory = status.Mandatory, recoveryCodesLeft = status.RecoveryCodesLeft });
    }

    private static async Task<IResult> StartAccountSetupAsync(HttpContext context, MfaService mfa, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var result = await mfa.StartAccountSetupAsync(user.OrganizationId, user.Id, cancellationToken).ConfigureAwait(false);
        return result.Succeeded
            ? Results.Ok(ToSetupResponse(result.Setup!))
            : Problem(result, IdentityApiText.Current.MfaNotChanged);
    }

    private static async Task<IResult> ConfirmAccountSetupAsync(HttpContext context, MfaService mfa, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var request = await RequestBody.ReadAsync<CodeRequest>(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var result = await mfa.ConfirmAccountSetupAsync(user.OrganizationId, user.Id, request.Code, AuthEndpoints.ClientKey(context), cancellationToken)
            .ConfigureAwait(false);
        return result.Succeeded
            ? Results.Ok(new { recoveryCodes = result.RecoveryCodes })
            : Problem(result, IdentityApiText.Current.MfaNotChanged);
    }

    private static async Task<IResult> DisableAsync(HttpContext context, MfaService mfa, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var request = await RequestBody.ReadAsync<CodeRequest>(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var result = await mfa.DisableAsync(user.OrganizationId, user.Id, request.Code, AuthEndpoints.ClientKey(context), cancellationToken)
            .ConfigureAwait(false);
        return result.Succeeded ? Results.NoContent() : Problem(result, IdentityApiText.Current.MfaNotChanged);
    }

    private static async Task<IResult> RegenerateAsync(HttpContext context, MfaService mfa, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var request = await RequestBody.ReadAsync<CodeRequest>(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var result = await mfa.RegenerateRecoveryCodesAsync(user.OrganizationId, user.Id, request.Code, AuthEndpoints.ClientKey(context), cancellationToken)
            .ConfigureAwait(false);
        return result.Succeeded
            ? Results.Ok(new { recoveryCodes = result.RecoveryCodes })
            : Problem(result, IdentityApiText.Current.MfaNotChanged);
    }

    // ── Platform ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ResetAsync(HttpContext context, MfaService mfa, CancellationToken cancellationToken)
    {
        var request = await RequestBody.ReadAsync<ResetRequest>(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var result = await mfa.ResetAsync(request.Email ?? string.Empty, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? Results.NoContent() : Problem(result, IdentityApiText.Current.MfaNotReset);
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────

    private static object ToSetupResponse(MfaSetupInfo setup) => new { secret = setup.Secret, otpauthUri = setup.OtpAuthUri };

    private static IResult Problem(MfaResult result, string title)
    {
        var status = result.LockedOut
            ? StatusCodes.Status429TooManyRequests
            : result.ChallengeGone
                ? StatusCodes.Status401Unauthorized
                : result.NotFound
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;

        return Results.Problem(detail: result.Error, title: title, statusCode: status);
    }
}
