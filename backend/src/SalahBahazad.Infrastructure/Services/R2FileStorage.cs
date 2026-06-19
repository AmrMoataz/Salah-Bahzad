using Amazon.S3;
using Amazon.S3.Model;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// <see cref="IFileStorage"/> over the AWS S3 SDK, targeting Cloudflare R2 (staging/prod) and MinIO
/// (dev/tests) through the same code path — the <see cref="IAmazonS3"/> client is configured with the
/// environment's endpoint/credentials in DI. Uploads land in the private bucket; reads are served only
/// via short-lived pre-signed URLs (FR-PLAT-AST-003/004). Object keys are never logged (minors' PII,
/// NFR-PRIV-001/002).
/// </summary>
internal sealed class R2FileStorage(IAmazonS3 s3, R2Options options, TimeProvider clock) : IFileStorage
{
    /// <summary>
    /// Multipart part size (8 MB). Above the S3 5 MB minimum for non-final parts, and small enough that
    /// peak memory for a streaming upload is ≈ one buffer regardless of total file size.
    /// </summary>
    private const int PartSize = 8 * 1024 * 1024;

    public async Task UploadPrivateAsync(
        string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var request = new PutObjectRequest
        {
            BucketName = options.BucketPrivate,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            // Bucket is private by default (R2 and our MinIO bucket) — no public-read ACL (FR-PLAT-AST-003).
        };

        await s3.PutObjectAsync(request, cancellationToken);
    }

    public async Task UploadPrivateStreamingAsync(
        string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var initiate = await s3.InitiateMultipartUploadAsync(
            new InitiateMultipartUploadRequest
            {
                BucketName = options.BucketPrivate,
                Key = key,
                ContentType = contentType,
            },
            cancellationToken);

        var parts = new List<PartETag>();
        var buffer = new byte[PartSize];

        try
        {
            var partNumber = 1;
            int read;
            do
            {
                // Fill a full part from the (possibly non-seekable) stream before sending it; a short
                // read only happens at the end of the stream.
                read = await ReadFullAsync(content, buffer, cancellationToken);

                // The buffer is reused each iteration; safe because UploadPart fully consumes it before we
                // loop. MemoryStream wraps the slice without copying (writable: false).
                using var partStream = new MemoryStream(buffer, 0, read, writable: false);
                var partResponse = await s3.UploadPartAsync(
                    new UploadPartRequest
                    {
                        BucketName = options.BucketPrivate,
                        Key = key,
                        UploadId = initiate.UploadId,
                        PartNumber = partNumber,
                        PartSize = read,
                        InputStream = partStream,
                    },
                    cancellationToken);

                parts.Add(new PartETag(partNumber, partResponse.ETag));
                partNumber++;
            }
            while (read == buffer.Length); // a part shorter than the buffer was the last one

            await s3.CompleteMultipartUploadAsync(
                new CompleteMultipartUploadRequest
                {
                    BucketName = options.BucketPrivate,
                    Key = key,
                    UploadId = initiate.UploadId,
                    PartETags = parts,
                },
                cancellationToken);
        }
        catch
        {
            // Reclaim the dangling multipart upload so half-uploaded parts aren't billed/left behind.
            // Best-effort and out-of-band of the cancelled token.
            await s3.AbortMultipartUploadAsync(
                new AbortMultipartUploadRequest
                {
                    BucketName = options.BucketPrivate,
                    Key = key,
                    UploadId = initiate.UploadId,
                },
                CancellationToken.None);
            throw;
        }
    }

    /// <summary>Reads until <paramref name="buffer"/> is full or the stream ends; returns the byte count.</summary>
    private static async Task<int> ReadFullAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0)
                break;
            total += read;
        }

        return total;
    }

    public async Task<SignedUrl> GetSignedReadUrlAsync(
        string key, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var effectiveTtl = ttl ?? TimeSpan.FromSeconds(options.SignedUrlTtlSeconds);
        // Expiry computed from the injected clock for deterministic, testable TTLs.
        var expiresAt = clock.GetUtcNow().Add(effectiveTtl);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = options.BucketPrivate,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime,
        };

        var url = await s3.GetPreSignedURLAsync(request);

        // The AWS SDK v4 presigner emits an https URL even when the endpoint is http (e.g. local
        // MinIO), which makes the link unreachable in dev/tests. The SigV4 signature covers the host
        // header, not the scheme, so aligning the scheme with the configured endpoint keeps it valid.
        // R2 is https in staging/prod, so this is a no-op there.
        if (options.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = string.Concat("http://", url.AsSpan("https://".Length));
        }

        return new SignedUrl(url, expiresAt);
    }
}
