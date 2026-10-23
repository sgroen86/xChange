using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Api.Organizations;

/// <summary>
/// Managing organisations across the platform. Every route requires a platform
/// administrator. None of them returns an organisation's invoices, relations or
/// settings: those remain readable only from inside the organisation. Deleting
/// one removes them unread.
/// </summary>
public static class PlatformEndpoints
{
    public static RouteGroupBuilder MapPlatformEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs).
        group.MapGet("/platform/organizations", ListAsync)
            .WithName("ListOrganizations")
            .RequirePlatformAdmin();

        group.MapPost("/platform/organizations", CreateAsync)
            .WithName("CreateOrganization")
            .RequirePlatformAdmin();

        group.MapPut("/platform/organizations/{organizationId}", UpdateAsync)
            .WithName("UpdateOrganization")
            .RequirePlatformAdmin();

        group.MapPost("/platform/organizations/{organizationId}/invitation", ReissueAsync)
            .WithName("ReissueInvitation")
            .RequirePlatformAdmin();

        group.MapDelete("/platform/organizations/{organizationId}", DeleteAsync)
            .WithName("DeleteOrganization")
            .RequirePlatformAdmin();

        return group;
    }

    private static async Task<IResult> ListAsync(PlatformService platform, CancellationToken cancellationToken)
    {
        var organizations = await platform.ListAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(organizations.Select(summary => ToResponse(summary.Organization, summary.UserCount, summary.InvitationPending)));
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        PlatformService platform,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var request = await RequestBody.ReadAsync<CreateOrganizationRequest>(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var result = await platform.CreateOrganizationAsync(
            user.Id,
            request.Name ?? string.Empty,
            request.CountryCode ?? string.Empty,
            request.FirstUser?.Name ?? string.Empty,
            request.FirstUser?.Email ?? string.Empty,
            cancellationToken).ConfigureAwait(false);

        return ToResult(result);
    }

    private static async Task<IResult> UpdateAsync(
        string organizationId,
        HttpContext context,
        PlatformService platform,
        CancellationToken cancellationToken)
    {
        var request = await RequestBody.ReadAsync<UpdateOrganizationRequest>(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var result = await platform.UpdateOrganizationAsync(
            organizationId, request.Name ?? string.Empty, request.CountryCode ?? string.Empty, cancellationToken).ConfigureAwait(false);

        return ToResult(result);
    }

    private static async Task<IResult> ReissueAsync(
        string organizationId,
        HttpContext context,
        PlatformService platform,
        CancellationToken cancellationToken)
    {
        var request = await RequestBody.ReadAsync<InviteeRequest>(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var result = await platform.ReissueInvitationAsync(
            organizationId, request.Name ?? string.Empty, request.Email ?? string.Empty, cancellationToken).ConfigureAwait(false);

        return ToResult(result);
    }

    /// <summary>
    /// Deletes the organisation and everything it has, irreversibly. The body
    /// repeats the organisation's name, as a confirmation the client cannot
    /// skip by accident.
    /// </summary>
    private static async Task<IResult> DeleteAsync(
        string organizationId,
        HttpContext context,
        OrganizationDeletionService deletion,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var request = await RequestBody.ReadAsync<DeleteOrganizationRequest>(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var result = await deletion.DeleteAsync(
            user.Id, user.OrganizationId, organizationId, request.ConfirmName, cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            OrganizationDeletionOutcome.Deleted => Results.NoContent(),
            OrganizationDeletionOutcome.NotFound => Results.NotFound(),
            OrganizationDeletionOutcome.Refused => Results.Problem(
                detail: result.Error, title: OrganizationApiText.Current.NotDeleted, statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(
                detail: result.Error, title: OrganizationApiText.Current.NotDeleted, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    private static IResult ToResult(PlatformResult result) => result.Outcome switch
    {
        PlatformOutcome.Succeeded => Results.Ok(new
        {
            organization = ToResponse(result.Organization!, userCount: null, invitationPending: result.Organization!.PendingInvitationHash is not null),
            // Returned once; only its hash is stored.
            invitationCode = result.InvitationCode,
        }),
        PlatformOutcome.NotFound => Results.NotFound(),
        _ => Results.Problem(detail: result.Error, title: OrganizationApiText.Current.NotSaved, statusCode: StatusCodes.Status400BadRequest),
    };

    private static object ToResponse(Organization organization, int? userCount, bool invitationPending) => new
    {
        organizationId = organization.OrganizationId,
        name = organization.Name,
        countryCode = organization.CountryCode,
        createdAt = organization.CreatedAt,
        userCount,
        invitationPending,
        pendingInvitationEmail = invitationPending ? organization.PendingInvitationEmail : null,
    };

    public sealed record InviteeRequest(string? Name, string? Email);

    public sealed record CreateOrganizationRequest(string? Name, string? CountryCode, InviteeRequest? FirstUser);

    public sealed record UpdateOrganizationRequest(string? Name, string? CountryCode);

    public sealed record DeleteOrganizationRequest(string? ConfirmName);
}
