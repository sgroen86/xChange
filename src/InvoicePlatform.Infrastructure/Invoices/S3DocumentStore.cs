using Amazon.S3;
using Amazon.S3.Model;
using InvoicePlatform.Application.Invoices;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Invoices;

/// <summary>
/// Source PDFs in S3.
///
/// The key starts with the organisation id, so an object listing is already
/// segmented by tenant. Callers never pass a key in from outside: it is read
/// back off the invoice record, which is itself scoped by organisation.
/// </summary>
internal sealed class S3DocumentStore(
    IAmazonS3 s3,
    IOptions<InvoiceStorageOptions> options)
    : IDocumentStore
{
    private readonly string _bucket = options.Value.BucketName;

    public async Task<string> PutAsync(
        string organizationId,
        string invoiceId,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var key = $"{organizationId}/{invoiceId}.pdf";

        using var stream = new MemoryStream(content.ToArray(), writable: false);

        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
            },
            cancellationToken).ConfigureAwait(false);

        return key;
    }

    public async Task<byte[]?> GetAsync(
        string documentKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await s3
                .GetObjectAsync(_bucket, documentKey, cancellationToken)
                .ConfigureAwait(false);

            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            return buffer.ToArray();
        }
        catch (AmazonS3Exception exception)
            when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // A missing document is an absent value, not an error for callers.
            return null;
        }
    }
}
