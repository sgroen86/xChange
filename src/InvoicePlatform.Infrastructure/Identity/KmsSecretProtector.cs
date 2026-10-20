using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using InvoicePlatform.Application.Identity;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Identity;

/// <summary>
/// Encrypts TOTP secrets with a KMS key (alias/xchange-mfa, infra/aws/service.yml).
/// The key never leaves KMS, so a copy of the identity table - a backup, an
/// export, a leaked read permission - holds no usable secret.
///
/// The context goes in as KMS encryption context: it is authenticated with the
/// ciphertext, so a secret copied onto another user's record fails to decrypt.
/// </summary>
internal sealed class KmsSecretProtector(
    IAmazonKeyManagementService kms,
    IOptions<MfaOptions> options) : ISecretProtector
{
    private const string ContextKey = "xchange:mfa-user";

    public async Task<string> ProtectAsync(byte[] plaintext, string context, CancellationToken cancellationToken = default)
    {
        using var input = new MemoryStream(plaintext, writable: false);
        var response = await kms.EncryptAsync(
            new EncryptRequest
            {
                KeyId = KeyId(),
                Plaintext = input,
                EncryptionContext = new Dictionary<string, string> { [ContextKey] = context },
            },
            cancellationToken).ConfigureAwait(false);

        return Convert.ToBase64String(response.CiphertextBlob.ToArray());
    }

    public async Task<byte[]> UnprotectAsync(string protectedValue, string context, CancellationToken cancellationToken = default)
    {
        using var input = new MemoryStream(Convert.FromBase64String(protectedValue), writable: false);
        var response = await kms.DecryptAsync(
            new DecryptRequest
            {
                // Named, so a ciphertext from any other key is refused rather than decrypted.
                KeyId = KeyId(),
                CiphertextBlob = input,
                EncryptionContext = new Dictionary<string, string> { [ContextKey] = context },
            },
            cancellationToken).ConfigureAwait(false);

        return response.Plaintext.ToArray();
    }

    private string KeyId()
        => string.IsNullOrWhiteSpace(options.Value.KmsKeyId)
            ? throw new InvalidOperationException("Mfa:KmsKeyId is not configured; two-step verification needs the KMS key from infra/aws/service.yml.")
            : options.Value.KmsKeyId;
}

public sealed class MfaOptions
{
    public const string SectionName = "Mfa";

    /// <summary>ARN or alias of the KMS key that protects TOTP secrets. The stack sets the ARN.</summary>
    public string? KmsKeyId { get; set; } = "alias/xchange-mfa";
}
