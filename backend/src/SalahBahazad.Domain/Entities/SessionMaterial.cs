using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A downloadable/readable attachment on a <see cref="Session"/> (FR-PLAT-SES-003, FR-ADM-SES-004):
/// PDF/CSV/PNG/JPG. A child of the Session aggregate — created and removed only through
/// <see cref="Session.AddMaterial"/> / <see cref="Session.RemoveMaterial"/>. The DB stores the R2 object
/// <b>key</b> only; bytes are served via a short-lived signed URL fetched on demand (FR-PLAT-AST-003/004).
/// </summary>
public sealed class SessionMaterial : EntityBase
{
    private SessionMaterial() { }

    public Guid SessionId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>R2 object key (private bucket) — never bytes or a durable URL (FR-PLAT-AST-004).</summary>
    public string ObjectKey { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    internal static SessionMaterial Create(
        Guid sessionId, string fileName, string contentType, string objectKey, long sizeBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        return new SessionMaterial
        {
            SessionId = sessionId,
            FileName = fileName.Trim(),
            ContentType = contentType.Trim(),
            ObjectKey = objectKey,
            SizeBytes = sizeBytes,
        };
    }
}
