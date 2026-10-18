using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Relations;

namespace InvoicePlatform.Application.Relations;

/// <summary>
/// The organisation's customers: remembered when an invoice is booked, looked
/// up when the next one is extracted, and correctable by hand.
/// </summary>
public sealed class RelationService(IRelationRepository relations, IClock clock)
{
    public Task<IReadOnlyList<Relation>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
        => relations.ListAsync(organizationId, cancellationToken);

    public async Task<Relation?> FindForAsync(
        string organizationId,
        CanonicalParty? party,
        CancellationToken cancellationToken = default)
    {
        if (!RelationKeys.IsIdentifiable(party))
        {
            return null;
        }

        var known = await relations.ListAsync(organizationId, cancellationToken).ConfigureAwait(false);
        return RelationKeys.FindMatch(known, party);
    }

    /// <summary>
    /// Stores the buyer of a booked invoice as a relation, or brings the
    /// existing one up to date with the values the invoice has. A buyer without
    /// a VAT or registration number is not stored: it could never be found again.
    /// </summary>
    public async Task RememberBuyerAsync(
        string organizationId,
        CanonicalParty? buyer,
        CancellationToken cancellationToken = default)
    {
        if (buyer is null || !RelationKeys.IsIdentifiable(buyer))
        {
            return;
        }

        var existing = await FindForAsync(organizationId, buyer, cancellationToken).ConfigureAwait(false);
        var relation = existing ?? new Relation
        {
            RelationId = Guid.NewGuid().ToString("n"),
            OrganizationId = organizationId,
            UpdatedAt = clock.UtcNow,
        };

        // Only values the invoice actually has: an empty field on one invoice
        // must not erase what an earlier one taught us.
        relation = relation with
        {
            Name = Clean(buyer.Name) ?? relation.Name,
            VatIdentifier = RelationKeys.NormalizeVat(buyer.VatIdentifier) ?? relation.VatIdentifier,
            LegalRegistrationId = RelationKeys.NormalizeRegistration(buyer.LegalRegistrationId) ?? relation.LegalRegistrationId,
            LegalRegistrationScheme = Clean(buyer.LegalRegistrationScheme) ?? relation.LegalRegistrationScheme,
            ElectronicAddress = Clean(buyer.ElectronicAddress) ?? relation.ElectronicAddress,
            ElectronicAddressScheme = Clean(buyer.ElectronicAddressScheme) ?? relation.ElectronicAddressScheme,
            CountryCode = Clean(buyer.Address?.CountryCode) ?? relation.CountryCode,
            UpdatedAt = clock.UtcNow,
        };

        await relations.SaveAsync(relation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A correction by hand. Not found when the relation belongs to another organisation.</summary>
    public async Task<RelationUpdateResult> UpdateAsync(
        string organizationId,
        string relationId,
        RelationUpdate update,
        CancellationToken cancellationToken = default)
    {
        var existing = await relations.GetAsync(organizationId, relationId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return RelationUpdateResult.NotFound;
        }

        var endpoint = Clean(update.ElectronicAddress);
        var endpointScheme = Clean(update.ElectronicAddressScheme);
        if (endpoint is not null && !CodeLists.IsElectronicAddressScheme(endpointScheme))
        {
            return RelationUpdateResult.Invalid("Kies een geldig schema voor het elektronisch adres.");
        }

        var registrationScheme = Clean(update.LegalRegistrationScheme);
        if (registrationScheme is not null && !CodeLists.IsRegistrationScheme(registrationScheme))
        {
            return RelationUpdateResult.Invalid("Het schema van het registratienummer is een code van vier cijfers, zoals 0106.");
        }

        var vat = RelationKeys.NormalizeVat(update.VatIdentifier);
        var registration = RelationKeys.NormalizeRegistration(update.LegalRegistrationId);
        if (vat is null && registration is null)
        {
            return RelationUpdateResult.Invalid("Een relatie heeft een btw-nummer of KvK-nummer nodig om herkend te worden.");
        }

        var updated = existing with
        {
            Name = Clean(update.Name),
            VatIdentifier = vat,
            LegalRegistrationId = registration,
            LegalRegistrationScheme = registration is null ? null : registrationScheme,
            ElectronicAddress = endpoint,
            ElectronicAddressScheme = endpoint is null ? null : endpointScheme,
            CountryCode = Clean(update.CountryCode)?.ToUpperInvariant(),
            UpdatedAt = clock.UtcNow,
        };

        await relations.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return new RelationUpdateResult(RelationUpdateOutcome.Updated, Relation: updated);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record RelationUpdate(
    string? Name,
    string? VatIdentifier,
    string? LegalRegistrationId,
    string? LegalRegistrationScheme,
    string? ElectronicAddress,
    string? ElectronicAddressScheme,
    string? CountryCode);

public enum RelationUpdateOutcome
{
    Updated,
    NotFound,
    Invalid,
}

public sealed record RelationUpdateResult(RelationUpdateOutcome Outcome, string? Error = null, Relation? Relation = null)
{
    public static readonly RelationUpdateResult NotFound = new(RelationUpdateOutcome.NotFound);

    public static RelationUpdateResult Invalid(string error) => new(RelationUpdateOutcome.Invalid, error);
}
