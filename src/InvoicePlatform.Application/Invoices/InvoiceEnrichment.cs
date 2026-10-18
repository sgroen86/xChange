using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Relations;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// Fills fields the PDF usually lacks from what the organisation already told
/// xChange: its own settings for the seller, its relations for the buyer.
/// Only empty fields: a value read from the document is never overwritten, and
/// where the document disagrees with a relation, the document wins and the
/// reviewer is told. Deterministic - no model is involved.
/// </summary>
public sealed class InvoiceEnrichment(IOrganizationSettingsStore settings, RelationService relations)
{
    public async Task<EnrichmentResult> EnrichAsync(
        string organizationId,
        CanonicalInvoiceDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var notices = new List<ValidationWarning>();

        draft = await FillSellerAsync(organizationId, draft, notices, cancellationToken).ConfigureAwait(false);

        var relation = await relations.FindForAsync(organizationId, draft.Buyer, cancellationToken).ConfigureAwait(false);
        draft = FillBuyer(draft, relation, notices);

        return new EnrichmentResult(draft, notices, relation);
    }

    /// <summary>The organisation is the seller: its own electronic address belongs there.</summary>
    private async Task<CanonicalInvoiceDraft> FillSellerAsync(
        string organizationId, CanonicalInvoiceDraft draft, List<ValidationWarning> notices, CancellationToken cancellationToken)
    {
        var organization = await settings.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var seller = draft.Seller ?? new CanonicalParty();

        if (!Empty(seller.ElectronicAddress)
            || organization?.EndpointId is not { } endpoint
            || organization.EndpointScheme is not { } scheme)
        {
            return draft;
        }

        notices.Add(Filled("ENRICH-ORGANIZATION", "seller.electronicAddress", EnrichmentText.Current.SellerEndpointFromSettings));
        return draft with { Seller = seller with { ElectronicAddress = endpoint, ElectronicAddressScheme = scheme } };
    }

    private static CanonicalInvoiceDraft FillBuyer(CanonicalInvoiceDraft draft, Relation? relation, List<ValidationWarning> notices)
    {
        var buyer = draft.Buyer;
        if (buyer is null || relation is null)
        {
            return draft;
        }

        var text = EnrichmentText.Current;

        // Electronic address and its scheme travel together.
        if (Empty(buyer.ElectronicAddress) && relation.ElectronicAddress is not null)
        {
            buyer = buyer with { ElectronicAddress = relation.ElectronicAddress, ElectronicAddressScheme = relation.ElectronicAddressScheme };
            notices.Add(Filled("ENRICH-RELATION", "buyer.electronicAddress", text.BuyerEndpointFromRelation));
        }
        else if (relation.ElectronicAddress is not null && buyer.ElectronicAddress!.Trim() != relation.ElectronicAddress)
        {
            notices.Add(Conflict("buyer.electronicAddress", text.Conflict(text.ElectronicAddress, relation.ElectronicAddress)));
        }

        if (Empty(buyer.LegalRegistrationId) && relation.LegalRegistrationId is not null)
        {
            buyer = buyer with { LegalRegistrationId = relation.LegalRegistrationId, LegalRegistrationScheme = relation.LegalRegistrationScheme };
            notices.Add(Filled("ENRICH-RELATION", "buyer.legalRegistrationId", text.BuyerRegistrationFromRelation));
        }
        else if (relation.LegalRegistrationId is not null
            && RelationKeys.NormalizeRegistration(buyer.LegalRegistrationId) != relation.LegalRegistrationId)
        {
            notices.Add(Conflict("buyer.legalRegistrationId", text.Conflict(text.Registration, relation.LegalRegistrationId)));
        }

        if (Empty(buyer.LegalRegistrationScheme) && !Empty(buyer.LegalRegistrationId) && relation.LegalRegistrationScheme is not null)
        {
            buyer = buyer with { LegalRegistrationScheme = relation.LegalRegistrationScheme };
        }

        if (Empty(buyer.VatIdentifier) && relation.VatIdentifier is not null)
        {
            buyer = buyer with { VatIdentifier = relation.VatIdentifier };
            notices.Add(Filled("ENRICH-RELATION", "buyer.vatIdentifier", text.BuyerVatFromRelation));
        }
        else if (relation.VatIdentifier is not null && RelationKeys.NormalizeVat(buyer.VatIdentifier) != relation.VatIdentifier)
        {
            notices.Add(Conflict("buyer.vatIdentifier", text.Conflict(text.Vat, relation.VatIdentifier)));
        }

        var address = buyer.Address ?? new CanonicalAddress();
        if (Empty(address.CountryCode) && relation.CountryCode is not null)
        {
            buyer = buyer with { Address = address with { CountryCode = relation.CountryCode } };
            notices.Add(Filled("ENRICH-RELATION", "buyer.address.countryCode", text.BuyerCountryFromRelation));
        }
        else if (relation.CountryCode is not null && address.CountryCode!.Trim().ToUpperInvariant() != relation.CountryCode)
        {
            notices.Add(Conflict("buyer.address.countryCode", text.Conflict(text.Country, relation.CountryCode)));
        }

        return draft with { Buyer = buyer };
    }

    private static bool Empty(string? value) => string.IsNullOrWhiteSpace(value);

    private static ValidationWarning Filled(string code, string path, string message)
        => new(code, path, message, ValidationSeverity.Info);

    private static ValidationWarning Conflict(string path, string message)
        => new("RELATION-CONFLICT", path, message, ValidationSeverity.Warning);
}

/// <param name="Notices">An Info warning per filled field and a Warning per conflict, for the review screen.</param>
/// <param name="Relation">The known customer the buyer was recognised as, if any.</param>
public sealed record EnrichmentResult(CanonicalInvoiceDraft Draft, IReadOnlyList<ValidationWarning> Notices, Relation? Relation = null);
