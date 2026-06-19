namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Object-storage seam for private assets (ID-verification images now; paid materials/videos in
/// Phase 3). One implementation — <c>R2FileStorage</c> over the AWS S3 SDK — serves every
/// environment: Cloudflare R2 in staging/prod and MinIO (S3-compatible) in dev and integration
/// tests, with only the endpoint/credentials differing.
///
/// The database stores object <b>keys</b> only, never bytes or durable public URLs (FR-PLAT-AST-004);
/// private bytes are retrieved exclusively via short-lived signed URLs issued after an authorised,
/// audited access (FR-PLAT-AST-003).
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Uploads <paramref name="content"/> to the private bucket under <paramref name="key"/>.
    /// The object is private by default — never public-read (FR-PLAT-AST-001/003).
    /// </summary>
    Task UploadPrivateAsync(
        string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a short-lived pre-signed GET URL for <paramref name="key"/>. When <paramref name="ttl"/>
    /// is null the configured default TTL is used (kept short for minors' PII). The caller MUST perform
    /// the authorisation check and write the access audit entry <b>before</b> calling this
    /// (FR-PLAT-AST-003, NFR-PRIV-001/002).
    /// </summary>
    Task<SignedUrl> GetSignedReadUrlAsync(
        string key, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
}

/// <summary>A pre-signed read URL and the instant it expires.</summary>
public sealed record SignedUrl(string Url, DateTimeOffset ExpiresAtUtc);
