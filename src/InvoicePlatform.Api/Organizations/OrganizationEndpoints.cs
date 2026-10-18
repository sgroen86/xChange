using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Contracts.Invoices;

namespace InvoicePlatform.Api.Organizations;

/// <summary>
/// The signed-in user's organisation settings. There is no organisation id in
/// the route: it comes from the session, so nobody can address another
/// tenant's settings.
/// </summary>
public static class OrganizationEndpoints
{
    public static RouteGroupBuilder MapOrganizationEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs).
        group.MapGet("/organization/settings", GetAsync)
            .WithName("GetOrganizationSettings")
            .RequireAuthenticatedUser();

        group.MapPut("/organization/settings", UpdateAsync)
            .WithName("UpdateOrganizationSettings")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin);

        return group;
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        OrganizationSettingsService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var settings = await service.GetAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false);
        var organization = await service.GetOrganizationAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(ToResponse(settings, service) with
        {
            OrganizationName = organization?.Name,
            CountryCode = organization?.CountryCode,
        });
    }

    private static async Task<IResult> UpdateAsync(
        HttpContext context,
        OrganizationSettingsService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;

        // Read here, not bound as a parameter: binding runs before the guards,
        // and an anonymous caller must get 401, not body-validation feedback.
        var request = await RequestBody.ReadAsync<UpdateOrganizationSettingsRequest>(context, cancellationToken)
            .ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var result = await service.UpdateAsync(
            user.OrganizationId,
            request.DefaultRulesetId,
            request.EndpointId,
            request.EndpointScheme,
            cancellationToken).ConfigureAwait(false);

        var organization = await service.GetOrganizationAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false);
        return result.Succeeded
            ? Results.Ok(ToResponse(result.Settings!, service) with
            {
                OrganizationName = organization?.Name,
                CountryCode = organization?.CountryCode,
            })
            : Results.Problem(detail: result.Error, title: "Instellingen niet opgeslagen", statusCode: StatusCodes.Status400BadRequest);
    }

    private static OrganizationSettingsResponse ToResponse(OrganizationSettings settings, OrganizationSettingsService service) => new()
    {
        DefaultRulesetId = settings.DefaultRulesetId,
        EndpointId = settings.EndpointId,
        EndpointScheme = settings.EndpointScheme,
        Rulesets = [.. service.Rulesets.Select(r => new RulesetInfo
        {
            Id = r.Id,
            DisplayName = r.DisplayName,
            RulesRelease = r.RulesRelease,
        })],
    };
}
