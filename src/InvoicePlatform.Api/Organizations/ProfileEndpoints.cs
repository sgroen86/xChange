using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Contracts.Organizations;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Api.Organizations;

/// <summary>
/// The signed-in user's company profile. There is no organisation id in the
/// route: it comes from the session, so nobody can address another tenant's.
/// </summary>
public static class ProfileEndpoints
{
    public static RouteGroupBuilder MapProfileEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs).
        group.MapGet("/organization/profile", GetAsync)
            .WithName("GetOrganizationProfile")
            .RequireAuthenticatedUser();

        group.MapPut("/organization/profile", UpdateAsync)
            .WithName("UpdateOrganizationProfile")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin);

        return group;
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        OrganizationProfileService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var profile = await service.GetAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(await ToResponseAsync(profile, service, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> UpdateAsync(
        HttpContext context,
        OrganizationProfileService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;

        // Read here, not bound as a parameter: binding runs before the guards,
        // and an anonymous caller must get 401, not body-validation feedback.
        var request = await RequestBody.ReadAsync<UpdateOrganizationProfileRequest>(context, cancellationToken)
            .ConfigureAwait(false);
        if (request is null || request.General is null || request.Peppol is null)
        {
            return RequestBody.Invalid();
        }

        var result = await service.UpdateAsync(user.OrganizationId, FromRequest(request), cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(
                result.Errors.GroupBy(e => e.Field).ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray()),
                title: OrganizationApiText.Current.ProfileNotSaved);
        }

        return Results.Ok(await ToResponseAsync(result.Profile!, service, cancellationToken).ConfigureAwait(false));
    }

    private static OrganizationProfile FromRequest(UpdateOrganizationProfileRequest request)
    {
        var general = request.General!;
        var peppol = request.Peppol!;
        return new OrganizationProfile
        {
            // The service puts the caller's organisation here; never the request's.
            OrganizationId = string.Empty,
            General = new CompanyDetails
            {
                LegalName = general.LegalName,
                VatIdentifier = general.VatIdentifier,
                LegalRegistrationId = general.LegalRegistrationId,
                LegalRegistrationScheme = general.LegalRegistrationScheme,
                StreetName = general.StreetName,
                AdditionalStreetName = general.AdditionalStreetName,
                PostalZone = general.PostalZone,
                CityName = general.CityName,
                ContactName = general.ContactName,
                ContactEmail = general.ContactEmail,
                ContactPhone = general.ContactPhone,
                PaymentMeansCode = general.PaymentMeansCode,
                Iban = general.Iban,
                Bic = general.Bic,
                AccountName = general.AccountName,
            },
            Peppol = new PeppolDetails { EndpointId = peppol.EndpointId, EndpointScheme = peppol.EndpointScheme },
        };
    }

    private static async Task<OrganizationProfileResponse> ToResponseAsync(
        OrganizationProfile profile,
        OrganizationProfileService service,
        CancellationToken cancellationToken)
    {
        var organization = await service.GetOrganizationAsync(profile.OrganizationId, cancellationToken).ConfigureAwait(false);
        var general = profile.General;
        return new OrganizationProfileResponse
        {
            OrganizationName = organization?.Name,
            CountryCode = organization?.CountryCode,
            General = new CompanyDetailsDto
            {
                LegalName = general.LegalName,
                VatIdentifier = general.VatIdentifier,
                LegalRegistrationId = general.LegalRegistrationId,
                LegalRegistrationScheme = general.LegalRegistrationScheme,
                StreetName = general.StreetName,
                AdditionalStreetName = general.AdditionalStreetName,
                PostalZone = general.PostalZone,
                CityName = general.CityName,
                ContactName = general.ContactName,
                ContactEmail = general.ContactEmail,
                ContactPhone = general.ContactPhone,
                PaymentMeansCode = general.PaymentMeansCode,
                Iban = general.Iban,
                Bic = general.Bic,
                AccountName = general.AccountName,
            },
            Peppol = new PeppolDetailsDto { EndpointId = profile.Peppol.EndpointId, EndpointScheme = profile.Peppol.EndpointScheme },
        };
    }
}
