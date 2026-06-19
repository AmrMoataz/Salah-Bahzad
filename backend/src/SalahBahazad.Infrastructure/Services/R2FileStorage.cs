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

    public Task<string> GetSignedReadUrlAsync(
        string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = options.BucketPrivate,
            Key = key,
            Verb = HttpVerb.GET,
            // Expiry computed from the injected clock for deterministic, testable TTLs.
            Expires = clock.GetUtcNow().Add(ttl).UtcDateTime,
        };

        return s3.GetPreSignedURLAsync(request);
    }
}
