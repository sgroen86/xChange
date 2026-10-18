using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Contracts.Relations;
using InvoicePlatform.Domain.Relations;

namespace InvoicePlatform.Api.Relations;

/// <summary>
/// The organisation's relations. The organisation comes from the session, and a
/// relation id from another organisation is simply not found.
/// </summary>
public static class RelationEndpoints
{
    public static RouteGroupBuilder MapRelationEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs).
        group.MapGet("/relations", ListAsync)
            .WithName("ListRelations")
            .RequireAuthenticatedUser();

        group.MapPut("/relations/{relationId}", UpdateAsync)
            .WithName("UpdateRelation")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin, UserRoleName.User);

        return group;
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        RelationService relations,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var list = await relations.ListAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false);

        return Results.Ok(list
            .OrderBy(r => r.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .Select(ToResponse));
    }

    private static async Task<IResult> UpdateAsync(
        string relationId,
        HttpContext context,
        RelationService relations,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;

        var request = await RequestBody.ReadAsync<UpdateRelationRequest>(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var result = await relations.UpdateAsync(
            user.OrganizationId,
            relationId,
            new RelationUpdate(
                request.Name,
                request.VatIdentifier,
                request.LegalRegistrationId,
                request.LegalRegistrationScheme,
                request.ElectronicAddress,
                request.ElectronicAddressScheme,
                request.CountryCode,
                request.DefaultRulesetId),
            cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            RelationUpdateOutcome.Updated => Results.Ok(ToResponse(result.Relation!)),
            RelationUpdateOutcome.Invalid => Results.Problem(
                detail: result.Error, title: "Relatie niet opgeslagen", statusCode: StatusCodes.Status400BadRequest),
            _ => Results.NotFound(),
        };
    }

    private static RelationResponse ToResponse(Relation relation) => new()
    {
        RelationId = relation.RelationId,
        Name = relation.Name,
        VatIdentifier = relation.VatIdentifier,
        LegalRegistrationId = relation.LegalRegistrationId,
        LegalRegistrationScheme = relation.LegalRegistrationScheme,
        ElectronicAddress = relation.ElectronicAddress,
        ElectronicAddressScheme = relation.ElectronicAddressScheme,
        CountryCode = relation.CountryCode,
        DefaultRulesetId = relation.DefaultRulesetId,
        UpdatedAt = relation.UpdatedAt,
    };
}
