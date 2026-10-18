namespace InvoicePlatform.Domain.Networks;

/// <summary>
/// An address on an e-invoicing network: an Electronic Address Scheme code
/// (ISO 6523 ICD or PEPPOL EAS, e.g. "0106" for a KvK number) and the value in
/// that scheme. Written as PEPPOL writes it: "0106:12345678".
/// </summary>
public sealed record ParticipantIdentifier(string Scheme, string Value)
{
    public override string ToString() => $"{Scheme}:{Value}";
}

/// <summary>
/// A document type a receiver accepts, as its network publishes it - for
/// PEPPOL the scheme ("busdox-docid-qns" or "peppol-doctype-wildcard") and
/// the identifier ("...Invoice-2::Invoice##&lt;customization&gt;::2.1").
/// </summary>
public sealed record DocumentTypeId(string Scheme, string Value);
