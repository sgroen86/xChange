using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Anthropic;

/// <summary>
/// Supplies the Anthropic API key, resolving it once and caching it.
///
/// Two sources, in order:
///
/// 1. Configuration - Anthropic:ApiKey, Anthropic__ApiKey, or ANTHROPIC_API_KEY.
///    This is the local development path.
/// 2. Secrets Manager, by ARN. This is the deployed path.
///
/// The secret is read at runtime rather than injected as an environment
/// variable, so the key never appears in the Lambda configuration, in the
/// CloudFormation template or its parameter history, or in a deploy log.
/// Anyone with lambda:GetFunctionConfiguration can read environment variables;
/// reading the secret requires the function's own role.
/// </summary>
public interface IAnthropicApiKeyProvider
{
    Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default);
}

internal sealed class AnthropicApiKeyProvider : IAnthropicApiKeyProvider, IDisposable
{
    private readonly AnthropicOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _cached;

    public AnthropicApiKeyProvider(IOptions<AnthropicOptions> options) => _options = options.Value;

    public async Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return _cached = _options.ApiKey!;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKeySecretArn))
        {
            throw new InvalidOperationException(
                "No Anthropic API key configured. Set Anthropic:ApiKey (user-secrets or the "
                    + "ANTHROPIC_API_KEY environment variable) for local development, or "
                    + "Anthropic:ApiKeySecretArn to read it from Secrets Manager.");
        }

        // One caller fetches; the rest wait and reuse the result. A cold Lambda
        // can take several concurrent requests at once.
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            using var client = new AmazonSecretsManagerClient();
            var response = await client.GetSecretValueAsync(
                new GetSecretValueRequest { SecretId = _options.ApiKeySecretArn },
                cancellationToken).ConfigureAwait(false);

            var secret = response.SecretString;

            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new InvalidOperationException(
                    $"Secret {_options.ApiKeySecretArn} is empty.");
            }

            if (secret.StartsWith("PLACEHOLDER", StringComparison.Ordinal))
            {
                // The deploy workflow creates the secret with a placeholder so the
                // stack has something to reference. Say so plainly rather than
                // letting Anthropic reject it as a malformed key.
                throw new InvalidOperationException(
                    "The Anthropic API key is still the placeholder created at deploy time. "
                        + "Set the real value with: aws secretsmanager put-secret-value "
                        + "--secret-id xchange/anthropic-api-key --secret-string '<key>'");
            }

            return _cached = secret;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
