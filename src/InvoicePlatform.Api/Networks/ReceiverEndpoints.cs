using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Networks;
using InvoicePlatform.Contracts.Networks;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Api.Networks;

/// <summary>
/// Whether a buyer can receive e-invoices, and in which formats. Signed-in
/// users only, so the API is not an open proxy onto the PEPPOL network.
/// Nothing is stored.
/// </summary>
public static class ReceiverEndpoints
{
    public static RouteGroupBuilder MapReceiverEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs).
        group.MapPost("/receivers/lookup", LookupAsync)
            .WithName("LookupReceiver")
            .RequireAuthenticatedUser();

        return group;
    }

    private static async Task<IResult> LookupAsync(
        HttpContext context,
        ReceiverLookupService lookup,
        CancellationToken cancellationToken)
    {
        var request = await RequestBody.ReadAsync<LookupReceiverRequest>(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var party = new CanonicalParty
        {
            VatIdentifier = request.VatIdentifier,
            LegalRegistrationId = request.LegalRegistrationId,
            LegalRegistrationScheme = request.LegalRegistrationScheme,
            ElectronicAddress = request.ElectronicAddress,
            ElectronicAddressScheme = request.ElectronicAddressScheme,
            Address = new CanonicalAddress { CountryCode = request.CountryCode },
        };

        var result = await lookup.LookupAsync(party, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new ReceiverLookupResponse
        {
            Status = result.Status switch
            {
                ReceiverLookupStatus.Found => "found",
                ReceiverLookupStatus.NotFound => "notFound",
                ReceiverLookupStatus.Unavailable => "unavailable",
                _ => "nothingToLookUp",
            },
            Network = result.Network,
            Participant = result.Participant is { } p ? new ParticipantInfo { Scheme = p.Scheme, Value = p.Value } : null,
            AcceptedRulesetIds = result.AcceptedRulesetIds ?? [],
            DocumentTypeCount = result.DocumentTypeCount,
            Tried = [.. result.Tried.Select(t => t.ToString())],
        });
    }
}
