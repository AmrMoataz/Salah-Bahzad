namespace SalahBahazad.Application.Common;

/// <summary>
/// Builds R2 object keys for uploaded assets. Keys are grouped by the owning entity so the bucket is
/// self-describing when browsing/debugging — you can see exactly which session, question, or student an
/// object belongs to:
/// <list type="bullet">
/// <item><c>sessions/{tenantId}/{sessionId}/{videos|thumbnails|materials}/{guid}.ext</c></item>
/// <item><c>sessions/{tenantId}/{sessionId}/questions/{questionId}/{guid}.ext</c></item>
/// <item><c>students/{tenantId}/{studentId}/id-images/{guid}.ext</c></item>
/// </list>
/// Keys never leave the backend (FR-PLAT-AST-004) and the extension is derived from the validated
/// content type / file name only. Keys are opaque to readers (stored in the DB, handed verbatim to the
/// presigner), so this layout can evolve without a data migration — existing objects keep their old keys.
/// </summary>
internal static class StorageKeys
{
    public static string SessionThumbnail(Guid tenantId, Guid sessionId, string contentType)
        => $"sessions/{tenantId}/{sessionId}/thumbnails/{Guid.CreateVersion7():n}{ImageExtension(contentType)}";

    public static string SessionVideo(Guid tenantId, Guid sessionId, string contentType)
        => $"sessions/{tenantId}/{sessionId}/videos/{Guid.CreateVersion7():n}{VideoExtension(contentType)}";

    public static string SessionMaterial(Guid tenantId, Guid sessionId, string fileName)
        => $"sessions/{tenantId}/{sessionId}/materials/{Guid.CreateVersion7():n}{FileExtension(fileName)}";

    public static string QuestionImage(Guid tenantId, Guid sessionId, Guid questionId, string contentType)
        => $"sessions/{tenantId}/{sessionId}/questions/{questionId}/{Guid.CreateVersion7():n}{ImageExtension(contentType)}";

    public static string StudentIdImage(Guid tenantId, Guid studentId, string contentType)
        => $"students/{tenantId}/{studentId}/id-images/{Guid.CreateVersion7():n}{ImageExtension(contentType)}";

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
