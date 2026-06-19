namespace SalahBahazad.Application.Common;

/// <summary>
/// Builds R2 object keys for Phase 3 assets, following the Phase 2 convention
/// (<c>RegisterStudentHandler.BuildObjectKey</c>): <c>sessions/{tenantId}/{kind}/{guid}.ext</c> and
/// <c>questions/{tenantId}/images/{guid}.ext</c>. Keys never leave the backend (FR-PLAT-AST-004) and the
/// extension is derived from the validated content type / file name only.
/// </summary>
internal static class StorageKeys
{
    public static string SessionThumbnail(Guid tenantId, string contentType)
        => $"sessions/{tenantId}/thumbnails/{Guid.CreateVersion7():n}{ImageExtension(contentType)}";

    public static string SessionVideo(Guid tenantId, string contentType)
        => $"sessions/{tenantId}/videos/{Guid.CreateVersion7():n}{VideoExtension(contentType)}";

    public static string SessionMaterial(Guid tenantId, string fileName)
        => $"sessions/{tenantId}/materials/{Guid.CreateVersion7():n}{FileExtension(fileName)}";

    public static string QuestionImage(Guid tenantId, string contentType)
        => $"questions/{tenantId}/images/{Guid.CreateVersion7():n}{ImageExtension(contentType)}";

    private static string ImageExtension(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".bin",
    };

    private static string VideoExtension(string contentType) => contentType switch
    {
        "video/mp4" => ".mp4",
        "video/quicktime" => ".mov",
        "video/webm" => ".webm",
        _ => ".bin",
    };

    private static string FileExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(extension) ? ".bin" : extension.ToLowerInvariant();
    }
}
