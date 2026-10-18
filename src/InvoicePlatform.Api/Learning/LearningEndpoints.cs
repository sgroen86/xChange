using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Contracts.Learning;
using InvoicePlatform.Domain.Learning;

namespace InvoicePlatform.Api.Learning;

/// <summary>
/// What xChange learned from the organisation's corrections, and forgetting
/// it. The organisation comes from the session; there is no way to address
/// another organisation's corrections.
/// </summary>
public static class LearningEndpoints
{
    public static RouteGroupBuilder MapLearningEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs).
        group.MapGet("/learning", ListAsync)
            .WithName("ListLearnedCorrections")
            .RequireAuthenticatedUser();

        group.MapDelete("/learning/{correctionId}", ForgetAsync)
            .WithName("ForgetLearnedCorrection")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin, UserRoleName.User);

        return group;
    }

    private static async Task<IResult> ListAsync(HttpContext context, LearningService learning, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var corrections = await learning.ListAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false);

        return Results.Ok(corrections.Select(c => new LearnedCorrectionResponse
        {
            Id = c.Id,
            Scope = c.Scope,
            Field = c.Field,
            From = c.From,
            To = c.To,
            Context = c.Context,
            Use = c.Use == CorrectionUse.Apply ? "apply" : "hint",
            Count = c.Count,
            LastSeen = c.LastSeen,
        }));
    }

    private static async Task<IResult> ForgetAsync(
        string correctionId,
        HttpContext context,
        LearningService learning,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        return await learning.ForgetAsync(user.OrganizationId, correctionId, cancellationToken).ConfigureAwait(false)
            ? Results.NoContent()
            : Results.NotFound();
    }
}
