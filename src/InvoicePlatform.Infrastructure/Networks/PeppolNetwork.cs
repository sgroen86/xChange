using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using DnsClient;
using DnsClient.Protocol;
using InvoicePlatform.Application.Networks;
using InvoicePlatform.Domain.Networks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Networks;

public sealed class PeppolOptions
{
    public const string SectionName = "Peppol";

    /// <summary>
    /// The SML zone participants are looked up in. OpenPeppol's own zone
    /// replaces the European Commission's (edelivery.tech.ec.europa.eu) from
    /// 1 September 2026; the test network is participant.sml.test.tech.peppol.org.
    /// </summary>
    public string SmlZone { get; set; } = "participant.sml.prod.tech.peppol.org";

    /// <summary>For one participant: the DNS question and the SMP request together.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// PEPPOL dynamic discovery, read-only: the SML tells where a participant's
/// SMP is (a U-NAPTR record under SHA-256/Base32 of its identifier), and the
/// SMP lists the document types it accepts. Both are public; no certificate
/// is needed to read them.
/// </summary>
public sealed class PeppolNetwork : IReceiverNetwork, IDisposable
{
    private const string IdentifierScheme = "iso6523-actorid-upis";
    private const int MaxResponseBytes = 2 * 1024 * 1024;

    private readonly PeppolOptions _options;
    private readonly ILogger<PeppolNetwork> _logger;
    private readonly LookupClient _dns;
    private readonly HttpClient _http;

    public PeppolNetwork(IOptions<PeppolOptions> options, ILogger<PeppolNetwork> logger)
    {
        _options = options.Value;
        _logger = logger;
        _dns = new LookupClient(new LookupClientOptions { UseCache = true, Timeout = TimeSpan.FromSeconds(2), Retries = 2 });

        // Redirects are followed by hand, so every hop is checked like the first.
        _http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = _options.Timeout };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("xChange/1.0 (+https://www.xchangeinvoice.com/)");
    }

    public string Name => "peppol";

    public async Task<NetworkAnswer> FindAsync(ParticipantIdentifier participant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(participant);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        try
        {
            var smp = await LocateSmpAsync(participant, timeout.Token).ConfigureAwait(false);
            return smp is null
                ? NetworkAnswer.NotRegistered
                : await ReadServiceGroupAsync(smp, participant, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DnsResponseException or HttpRequestException or OperationCanceledException
            or System.Xml.XmlException or InvalidOperationException or SocketException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            _logger.LogWarning(exception, "PEPPOL lookup of {Participant} failed.", participant);
            return NetworkAnswer.Unavailable;
        }
    }

    /// <summary>The SMP base URL from the participant's U-NAPTR record; null when the SML does not know it.</summary>
    private async Task<Uri?> LocateSmpAsync(ParticipantIdentifier participant, CancellationToken cancellationToken)
    {
        var name = $"{Hash(participant)}.{IdentifierScheme}.{_options.SmlZone}";
        var response = await _dns.QueryAsync(name, QueryType.NAPTR, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Header.ResponseCode == DnsHeaderResponseCode.NotExistentDomain)
        {
            return null;
        }

        if (response.HasError)
        {
            throw new InvalidOperationException($"SML answered {response.Header.ResponseCode} for {name}.");
        }

        var record = response.Answers.OfType<NAPtrRecord>()
            .Where(r => string.Equals(r.Services, "Meta:SMP", StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.Flags, "U", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Order)
            .ThenBy(r => r.Preference)
            .FirstOrDefault();

        return record is null ? null : SmpUri(record.RegularExpression);
    }

    /// <summary>
    /// Lower-case identifier value ("0106:12345678"), SHA-256, Base32 without
    /// padding, lower case - the PEPPOL SML naming since the NAPTR migration.
    /// </summary>
    internal static string Hash(ParticipantIdentifier participant)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(participant.ToString().ToLowerInvariant()));
        return Base32(digest).ToLowerInvariant();
    }

    private static string Base32(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            output.Append(alphabet[(buffer << (5 - bits)) & 31]);
        }

        return output.ToString();
    }

    /// <summary>"!.*!https://smp.example.com/!" -> https://smp.example.com/.</summary>
    internal static Uri? SmpUri(string? regularExpression)
    {
        if (string.IsNullOrEmpty(regularExpression) || regularExpression.Length < 3)
        {
            return null;
        }

        var parts = regularExpression.Split(regularExpression[0]);
        return parts.Length >= 3 && Uri.TryCreate(parts[2], UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? uri
                : null;
    }

    private async Task<NetworkAnswer> ReadServiceGroupAsync(Uri smp, ParticipantIdentifier participant, CancellationToken cancellationToken)
    {
        var path = Uri.EscapeDataString($"{IdentifierScheme}::{participant}");
        var url = new Uri(smp, smp.AbsolutePath.TrimEnd('/') + "/" + path);

        for (var hop = 0; hop < 3; hop++)
        {
            await EnsurePublicAsync(url, cancellationToken).ConfigureAwait(false);

            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                url = location.IsAbsoluteUri ? location : new Uri(url, location);
                continue;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return NetworkAnswer.NotRegistered;
            }

            response.EnsureSuccessStatusCode();

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var limited = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (limited.Length + read > MaxResponseBytes)
                {
                    throw new InvalidOperationException("SMP response too large.");
                }

                limited.Write(buffer, 0, read);
            }

            limited.Position = 0;
            return NetworkAnswer.Registered(DocumentTypes(XDocument.Load(limited)));
        }

        throw new InvalidOperationException("Too many redirects from the SMP.");
    }

    /// <summary>The document types in a PEPPOL SMP 1.x ServiceGroup, from its service metadata references.</summary>
    internal static IReadOnlyList<DocumentTypeId> DocumentTypes(XDocument serviceGroup)
    {
        var types = new List<DocumentTypeId>();
        foreach (var reference in serviceGroup.Descendants().Where(e => e.Name.LocalName == "ServiceMetadataReference"))
        {
            var href = reference.Attribute("href")?.Value;
            var marker = href?.LastIndexOf("/services/", StringComparison.OrdinalIgnoreCase) ?? -1;
            if (marker < 0)
            {
                continue;
            }

            var documentType = Uri.UnescapeDataString(href![(marker + "/services/".Length)..]);
            var separator = documentType.IndexOf("::", StringComparison.Ordinal);
            if (separator > 0)
            {
                types.Add(new DocumentTypeId(documentType[..separator], documentType[(separator + 2)..]));
            }
        }

        return types;
    }

    /// <summary>
    /// The SMP address comes from public DNS, so it is checked before it is
    /// requested: only http(s) to public addresses, never the function's own
    /// network.
    /// </summary>
    private static async Task EnsurePublicAsync(Uri url, CancellationToken cancellationToken)
    {
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"SMP address {url} is not http(s).");
        }

        var addresses = IPAddress.TryParse(url.Host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(url.Host, cancellationToken).ConfigureAwait(false);

        if (addresses.Length == 0 || addresses.Any(IsPrivate))
        {
            throw new InvalidOperationException($"SMP address {url.Host} is not a public address.");
        }
    }

    internal static bool IsPrivate(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal || address.IsIPv6Multicast;
        }

        var b = address.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || b[0] == 0
            || b[0] >= 224;
    }

    public void Dispose() => _http.Dispose();
}
