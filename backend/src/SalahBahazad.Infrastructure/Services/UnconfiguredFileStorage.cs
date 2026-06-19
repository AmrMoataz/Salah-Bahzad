using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Registered in place of <see cref="R2FileStorage"/> when no <c>R2</c> configuration is present, so
/// the app still boots (e.g. integration tests that don't exercise storage). Any actual use fails
/// fast with a clear message rather than silently mis-storing assets. Dev gets MinIO config from the
/// AppHost; staging/prod get real R2 credentials from the environment.
/// </summary>
internal sealed class UnconfiguredFileStorage : IFileStorage
{
    private const string Message =
        "Object storage is not configured. Set R2__Endpoint, R2__AccessKeyId, R2__SecretAccessKey, " +
        "and R2__BucketPrivate (dev gets these from the Aspire AppHost's MinIO container).";

    public Task UploadPrivateAsync(
        string key, Stream content, string contentType, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(Message);

    public Task<SignedUrl> GetSignedReadUrlAsync(
        string key, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(Message);
}
