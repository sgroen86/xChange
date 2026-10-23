using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Billing;

/// <summary>Configuration for payments through Stripe.</summary>
public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>Local development only; deployed, the key comes from <see cref="SecretId"/>.</summary>
    public string? SecretKey { get; set; }

    /// <summary>Local development and tests only; deployed, it comes from <see cref="SecretId"/>.</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// Secrets Manager name or ARN of {"secretKey": "...", "webhookSecret": "..."}
    /// (xchange/stripe), read at runtime by the function's own role.
    /// </summary>
    public string? SecretId { get; set; }
}

/// <summary>The Stripe keys; either may be missing while payments are being set up.</summary>
internal sealed record StripeKeys(string? SecretKey, string? WebhookSecret);

/// <summary>
/// Reads the Stripe keys, like the Anthropic key: configuration first (local
/// development), else the secret in Secrets Manager, so the keys never sit in
/// the function's configuration or a deploy log.
///
/// A complete secret is kept for the life of the process. An incomplete or
/// unreadable one is read again after a few minutes, so adding the webhook
/// secret later takes effect without a deploy.
/// </summary>
internal sealed class StripeSecrets(IOptions<StripeOptions> options, ILogger<StripeSecrets> logger) : IDisposable
{
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(5);

    private readonly StripeOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private StripeKeys? _cached;
    private DateTimeOffset _readAt;

    public async Task<StripeKeys> GetAsync(CancellationToken cancellationToken = default)
    {
        if (Fresh(_cached) is { } cached)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Fresh(_cached) is { } again)
            {
                return again;
            }

            var stored = await ReadSecretAsync(cancellationToken).ConfigureAwait(false);
            _cached = new StripeKeys(
                Blank(_options.SecretKey) ?? stored?.SecretKey,
                Blank(_options.WebhookSecret) ?? stored?.WebhookSecret);
            _readAt = DateTimeOffset.UtcNow;
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    private StripeKeys? Fresh(StripeKeys? keys)
        => keys is null ? null
            : keys.SecretKey is not null && keys.WebhookSecret is not null ? keys
            : DateTimeOffset.UtcNow - _readAt < RetryAfter ? keys
            : null;

    private async Task<StripeKeys?> ReadSecretAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretId))
        {
            return null;
        }

        try
        {
            using var client = new AmazonSecretsManagerClient();
            var response = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = _options.SecretId }, cancellationToken)
                .ConfigureAwait(false);

            using var document = JsonDocument.Parse(response.SecretString ?? "{}");
            return new StripeKeys(Read(document, "secretKey"), Read(document, "webhookSecret"));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Not created yet, not readable, not JSON, or AWS unreachable: payments
            // stay off (503) and Stripe keeps retrying its webhooks.
            logger.LogWarning(exception, "The Stripe secret {SecretId} could not be read; payments are unavailable", _options.SecretId);
            return null;
        }
    }

    private static string? Read(JsonDocument document, string name)
        => document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
                ? Blank(value.GetString())
                : null;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void Dispose() => _gate.Dispose();
}
