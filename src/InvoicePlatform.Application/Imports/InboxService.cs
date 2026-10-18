using System.Security.Cryptography;
using InvoicePlatform.Domain.Imports;

namespace InvoicePlatform.Application.Imports;

/// <summary>Which organisation an inbox address belongs to. Platform data: the address, not the mail.</summary>
public interface IInboxDirectory
{
    Task<string?> FindOrganizationAsync(string localPart, CancellationToken cancellationToken = default);

    Task<string?> GetLocalPartAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>Gives the organisation a new address; the previous one stops working.</summary>
    Task AssignAsync(string organizationId, string localPart, string? previousLocalPart, CancellationToken cancellationToken = default);
}

/// <summary>A received email, as far as xChange cares: who, what about, and its PDF attachments.</summary>
public sealed record ReceivedMail(string? Sender, string? Subject, IReadOnlyList<MailAttachment> Attachments);

public sealed record MailAttachment(string FileName, byte[] Content);

/// <summary>Where received mail is kept until it is read (S3, filled by SES).</summary>
public interface IReceivedMailSource
{
    Task<ReceivedMail?> ReadAsync(string messageId, CancellationToken cancellationToken = default);
}

/// <param name="Domain">The inbox domain, e.g. "inbox.greenitsolutions.net".</param>
public sealed record InboxSettings(string Domain);

/// <summary>
/// Email import. Each organisation gets an address that is hard to guess
/// (<c>&lt;code&gt;@inbox.greenitsolutions.net</c>) and can be replaced when it
/// attracts unwanted mail. Every PDF attached to a mail sent there becomes an
/// import in that organisation's "to review" list, read in the background like
/// a batch upload.
/// </summary>
public sealed class InboxService(IInboxDirectory directory, IReceivedMailSource mail, ImportService imports, InboxSettings settings)
{
    /// <summary>More than this many PDFs in one mail is not an invoice run; the rest are ignored.</summary>
    public const int MaxAttachments = 20;

    /// <summary>Anthropic reads PDFs up to 32 MB; leave room.</summary>
    public const int MaxAttachmentBytes = 25 * 1024 * 1024;

    private static ReadOnlySpan<byte> PdfMagic => "%PDF-"u8;

    public async Task<string?> GetAddressAsync(string organizationId, CancellationToken cancellationToken = default)
        => await directory.GetLocalPartAsync(organizationId, cancellationToken).ConfigureAwait(false) is { } local
            ? $"{local}@{settings.Domain}"
            : null;

    /// <summary>A new address for the organisation; an earlier one stops receiving.</summary>
    public async Task<string> CreateAddressAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var previous = await directory.GetLocalPartAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var local = NewLocalPart();
        await directory.AssignAsync(organizationId, local, previous, cancellationToken).ConfigureAwait(false);
        return $"{local}@{settings.Domain}";
    }

    /// <summary>
    /// Turns a received mail into imports: for every recipient that is an
    /// organisation's inbox, every PDF attachment. Unknown recipients and
    /// non-PDF attachments are ignored.
    /// </summary>
    /// <returns>How many imports were created.</returns>
    public async Task<int> ReceiveAsync(string messageId, IEnumerable<string> recipients, CancellationToken cancellationToken = default)
    {
        var organizations = new List<string>();
        foreach (var recipient in recipients)
        {
            var at = recipient.LastIndexOf('@');
            if (at <= 0 || !string.Equals(recipient[(at + 1)..], settings.Domain, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (await directory.FindOrganizationAsync(recipient[..at].ToLowerInvariant(), cancellationToken).ConfigureAwait(false) is { } organization
                && !organizations.Contains(organization))
            {
                organizations.Add(organization);
            }
        }

        if (organizations.Count == 0)
        {
            return 0;
        }

        var received = await mail.ReadAsync(messageId, cancellationToken).ConfigureAwait(false);
        if (received is null)
        {
            return 0;
        }

        var pdfs = received.Attachments
            .Where(a => a.Content.Length is > 0 and <= MaxAttachmentBytes && a.Content.AsSpan().StartsWith(PdfMagic))
            .Take(MaxAttachments)
            .ToList();

        var created = 0;
        foreach (var organization in organizations)
        {
            foreach (var pdf in pdfs)
            {
                await imports.SubmitAsync(organization, ImportSource.Email, pdf.FileName, pdf.Content,
                    sender: received.Sender, subject: received.Subject, cancellationToken: cancellationToken).ConfigureAwait(false);
                created++;
            }
        }

        return created;
    }

    /// <summary>12 characters of base32 (60 bits): unguessable, readable, safe in an email address.</summary>
    private static string NewLocalPart()
    {
        const string alphabet = "abcdefghijkmnpqrstuvwxyz23456789";
        return string.Create(12, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }
        });
    }
}
