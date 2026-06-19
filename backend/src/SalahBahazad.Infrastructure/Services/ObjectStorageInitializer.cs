using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Dev-only bootstrap that ensures the private bucket exists in the local MinIO container, so a fresh
/// environment works without manual setup. Mirrors the <c>DatabaseSeeder</c> pattern: invoked from the
/// API's Development-only startup block, never in staging/prod (where R2 buckets are pre-created in
/// Cloudflare and the app is not permitted to create them). A failure is logged, never fatal.
/// </summary>
public static class ObjectStorageInitializer
{
    public static async Task EnsureBucketsAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ObjectStorageInitializer));

        var options = sp.GetService<R2Options>();
        if (options is null || !options.IsConfigured)
        {
            logger.LogInformation("Object storage not configured — skipping dev bucket bootstrap.");
            return;
        }

        var s3 = sp.GetRequiredService<IAmazonS3>();

        var exists = await AmazonS3Util.DoesS3BucketExistV2Async(s3, options.BucketPrivate);
        if (exists)
        {
            logger.LogInformation("Dev private bucket {Bucket} already exists.", options.BucketPrivate);
            return;
        }

        await s3.PutBucketAsync(new PutBucketRequest { BucketName = options.BucketPrivate }, ct);
        logger.LogInformation("Created dev private bucket {Bucket}.", options.BucketPrivate);
    }
}
