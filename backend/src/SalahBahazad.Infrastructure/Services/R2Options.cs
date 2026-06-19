namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Object-storage configuration, bound from the <c>R2</c> section (env only, NFR-SEC-002).
/// One shape for both backends: Cloudflare R2 (staging/prod) and MinIO (dev/tests) — only the
/// endpoint and credentials differ. Keys: <c>R2__Endpoint</c>, <c>R2__AccessKeyId</c>,
/// <c>R2__SecretAccessKey</c>, <c>R2__BucketPrivate</c>, <c>R2__SignedUrlTtlSeconds</c>.
/// </summary>
public sealed class R2Options
{
    public const string SectionName = "R2";

    /// <summary>S3-compatible service URL (R2 account endpoint, or the MinIO container endpoint in dev).</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>Private bucket for sensitive assets (ID images, paid materials) — never public-read.</summary>
    public string BucketPrivate { get; set; } = string.Empty;

    /// <summary>Default TTL for issued pre-signed read URLs. Kept short for minors' PII (NFR-PRIV-001/002).</summary>
    public int SignedUrlTtlSeconds { get; set; } = 300;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(AccessKeyId)
        && !string.IsNullOrWhiteSpace(SecretAccessKey)
        && !string.IsNullOrWhiteSpace(BucketPrivate);
}
