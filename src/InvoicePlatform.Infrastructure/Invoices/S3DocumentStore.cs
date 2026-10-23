using Amazon.S3;
using Amazon.S3.Model;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Organizations;
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
    : IDocumentStore, IOrganizationDocumentDeletion
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

    public Task DeleteAsync(string documentKey, CancellationToken cancellationToken = default)
        => s3.DeleteObjectAsync(_bucket, documentKey, cancellationToken);

    /// <summary>
    /// Every object under "&lt;organizationId&gt;/": invoices' and imports' PDFs
    /// alike. Listed a page (at most 1,000 keys) at a time and deleted with one
    /// DeleteObjects per page.
    /// </summary>
    public async Task<int> DeleteAllForOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        // An empty id would make the prefix "/" and a slash would reach into
        // another prefix; neither is an organisation.
        if (string.IsNullOrWhiteSpace(organizationId) || organizationId.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException("Not an organisation id.", nameof(organizationId));
        }

        var prefix = $"{organizationId}/";
        var deleted = 0;
        string? continuation = null;
        do
        {
            var page = await s3.ListObjectsV2Async(
                new ListObjectsV2Request { BucketName = _bucket, Prefix = prefix, ContinuationToken = continuation },
                cancellationToken).ConfigureAwait(false);

            var keys = (page.S3Objects ?? [])
                .Where(o => o.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(o => new KeyVersion { Key = o.Key })
                .ToList();
            if (keys.Count > 0)
            {
                var result = await s3.DeleteObjectsAsync(
                    new DeleteObjectsRequest { BucketName = _bucket, Objects = keys, Quiet = true },
                    cancellationToken).ConfigureAwait(false);

                if (result.DeleteErrors is { Count: > 0 } errors)
                {
                    throw new InvalidOperationException(
                        $"{errors.Count} documents of the organisation could not be deleted ({errors[0].Code}); run the deletion again.");
                }

                deleted += keys.Count;
            }

            continuation = page.IsTruncated == true ? page.NextContinuationToken : null;
        }
        while (continuation is not null);

        return deleted;
    }
}
