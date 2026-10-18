using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// Fills fields the PDF usually lacks from what the organisation already told
/// xChange. Only empty fields: a value read from the document is never
/// overwritten. Deterministic - no model is involved.
/// </summary>
public sealed class InvoiceEnrichment(IOrganizationSettingsStore settings)
{
    public async Task<EnrichmentResult> EnrichAsync(
        string organizationId,
        CanonicalInvoiceDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var notices = new List<ValidationWarning>();
        var organization = await settings.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);

        // The organisation is the seller: its own electronic address belongs there.
        var seller = draft.Seller ?? new CanonicalParty();
        if (string.IsNullOrWhiteSpace(seller.ElectronicAddress)
            && organization?.EndpointId is { } endpoint
            && organization.EndpointScheme is { } scheme)
        {
            draft = draft with { Seller = seller with { ElectronicAddress = endpoint, ElectronicAddressScheme = scheme } };
            notices.Add(new ValidationWarning(
                "ENRICH-ORGANIZATION",
                "seller.electronicAddress",
                "Elektronisch adres van de verkoper ingevuld uit de organisatie-instellingen.",
                ValidationSeverity.Info));
        }

        return new EnrichmentResult(draft, notices);
    }
}

/// <param name="Notices">One Info warning per filled field, for the review screen.</param>
public sealed record EnrichmentResult(CanonicalInvoiceDraft Draft, IReadOnlyList<ValidationWarning> Notices);
