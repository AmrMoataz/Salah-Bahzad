using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A teachable session: the catalogue/authoring unit that owns an ordered list of videos, downloadable
/// materials, and an optional gating-quiz configuration (FR-PLAT-SES-001..006, FR-ADM-SES-002..006).
/// Tenant-scoped and soft-deleted so enrollment/history survive a delete (FR-PLAT-ROLE-004,
/// FR-ADM-SES-011). The question bank (<see cref="Question"/>) references the session by id but is a
/// separate aggregate (paged independently), so it is not a child collection here.
/// </summary>
public sealed class Session : TenantEntityBase, ISoftDeletable
{
    /// <summary>Maximum enrollment-validity window in days (FR-PLAT-SES-001).</summary>
    public const int MaxValidityDays = 365;

    private readonly List<SessionVideo> _videos = [];
    private readonly List<SessionMaterial> _materials = [];

    private Session() { }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }

    /// <summary>R2 object key for the thumbnail; stored for the future student catalogue (FR-PLAT-SES-008).</summary>
    public string? ThumbnailObjectKey { get; private set; }

    /// <summary>Days an enrollment stays valid after purchase, 0–365 (FR-PLAT-SES-001).</summary>
    public int ValidityDays { get; private set; }

    /// <summary>Tenant-managed grade (FR-PLAT-TAX-001).</summary>
    public Guid GradeId { get; private set; }

    /// <summary>Tenant-managed specialization; its subject is derived via <see cref="Specialization.SubjectId"/>.</summary>
    public Guid SpecializationId { get; private set; }

    public SessionStatus Status { get; private set; } = SessionStatus.Draft;

    /// <summary>Optional prerequisite session that gates enrollment (FR-PLAT-SES-004, FR-PLAT-ENR-007).</summary>
    public Guid? PrerequisiteSessionId { get; private set; }

    /// <summary>Gating-quiz configuration owned 1:1; null until staff configure it (FR-PLAT-SES-006).</summary>
    public QuizSetting? QuizSetting { get; private set; }

    public IReadOnlyCollection<SessionVideo> Videos => _videos.AsReadOnly();
    public IReadOnlyCollection<SessionMaterial> Materials => _materials.AsReadOnly();

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public Guid? DeletedById { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static Session Create(
        Guid tenantId,
        string title,
        string? description,
        decimal price,
        int validityDays,
        Guid gradeId,
        Guid specializationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ValidatePrice(price);
        ValidateValidityDays(validityDays);
        if (gradeId == Guid.Empty) throw new ArgumentException("A session must have a grade.", nameof(gradeId));
        if (specializationId == Guid.Empty)
            throw new ArgumentException("A session must have a specialization.", nameof(specializationId));

        var session = new Session
        {
            Title = title.Trim(),
            Description = Normalize(description),
            Price = price,
            ValidityDays = validityDays,
            GradeId = gradeId,
            SpecializationId = specializationId,
            Status = SessionStatus.Draft,
        };
        session.SetTenant(tenantId);
        session.AddDomainEvent(new SessionCreatedEvent(session.Id, session.Title));
        return session;
    }

    /// <summary>Updates the core authoring details (FR-ADM-SES-002).</summary>
    public void UpdateDetails(
        string title, string? description, decimal price, int validityDays, Guid gradeId, Guid specializationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ValidatePrice(price);
        ValidateValidityDays(validityDays);
        if (gradeId == Guid.Empty) throw new ArgumentException("A session must have a grade.", nameof(gradeId));
        if (specializationId == Guid.Empty)
            throw new ArgumentException("A session must have a specialization.", nameof(specializationId));

        Title = title.Trim();
        Description = Normalize(description);
        Price = price;
        ValidityDays = validityDays;
        GradeId = gradeId;
        SpecializationId = specializationId;
    }

    /// <summary>Records the R2 key of the uploaded thumbnail (FR-ADM-SES-002).</summary>
    public void SetThumbnail(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ThumbnailObjectKey = objectKey;
    }

    /// <summary>
    /// Sets or clears the prerequisite (FR-ADM-SES-005). Blocks a <b>direct</b> self-reference; the full
    /// cycle walk across the chain is the handler's responsibility (it needs to read other sessions).
    /// </summary>
    public void SetPrerequisite(Guid? prerequisiteSessionId)
    {
        if (prerequisiteSessionId == Id)
            throw new InvalidOperationException("A session cannot be its own prerequisite (FR-ADM-SES-005).");

        PrerequisiteSessionId = prerequisiteSessionId == Guid.Empty ? null : prerequisiteSessionId;
    }

    /// <summary>Sets/replaces the gating-quiz configuration (FR-PLAT-SES-006, FR-ADM-QZ-001).</summary>
    public void UpdateQuizSettings(int timeLimitMinutes, int questionCount, int attemptCount, int minPassPercent)
        => QuizSetting = QuizSetting.Create(timeLimitMinutes, questionCount, attemptCount, minPassPercent);

    /// <summary>Publishes the session to the catalogue (FR-PLAT-SES-008). Already-published is illegal (→409).</summary>
    public void Publish()
    {
        if (Status == SessionStatus.Published)
            throw new InvalidOperationException("This session is already published.");

        Status = SessionStatus.Published;
        AddDomainEvent(new SessionPublishedEvent(Id));
    }

    /// <summary>Archives/retires the session (FR-PLAT-SES-001). Already-archived is illegal (→409).</summary>
    public void Archive()
    {
        if (Status == SessionStatus.Archived)
            throw new InvalidOperationException("This session is already archived.");

        Status = SessionStatus.Archived;
        AddDomainEvent(new SessionArchivedEvent(Id));
    }

    public void SoftDelete(Guid deletedById, DateTimeOffset now)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedById = deletedById;
        DeletedAtUtc = now;
        AddDomainEvent(new SessionDeletedEvent(Id));
    }

    // ── Videos (FR-PLAT-SES-002, FR-ADM-SES-003) ───────────────────────────────
    public SessionVideo AddVideo(string title, int lengthMinutes, int accessCount, string sourceObjectKey)
    {
        var nextOrder = _videos.Count == 0 ? 0 : _videos.Max(v => v.Order) + 1;
        var video = SessionVideo.Create(Id, title, nextOrder, lengthMinutes, accessCount, sourceObjectKey);
        _videos.Add(video);
        return video;
    }

    /// <summary>Edits a video's metadata and, when a new source is uploaded, replaces it (re-transcodes).</summary>
    public SessionVideo UpdateVideo(
        Guid videoId, string title, int lengthMinutes, int accessCount, string? newSourceObjectKey)
    {
        var video = FindVideo(videoId);
        video.UpdateMetadata(title, lengthMinutes, accessCount);
        if (!string.IsNullOrWhiteSpace(newSourceObjectKey))
            video.ReplaceSource(newSourceObjectKey);
        return video;
    }

    /// <summary>Reassigns video order from a complete, exactly-matching id list (FR-ADM-SES-003).</summary>
    public void ReorderVideos(IReadOnlyList<Guid> orderedVideoIds)
    {
        ArgumentNullException.ThrowIfNull(orderedVideoIds);
        var current = _videos.Select(v => v.Id).ToHashSet();
        if (orderedVideoIds.Count != _videos.Count || !orderedVideoIds.ToHashSet().SetEquals(current))
            throw new InvalidOperationException("The reorder list must contain exactly the session's video ids.");

        for (var i = 0; i < orderedVideoIds.Count; i++)
            FindVideo(orderedVideoIds[i]).SetOrder(i);
    }

    public SessionVideo RemoveVideo(Guid videoId)
    {
        var video = FindVideo(videoId);
        _videos.Remove(video);
        return video;
    }

    // ── Materials (FR-PLAT-SES-003, FR-ADM-SES-004) ────────────────────────────
    public SessionMaterial AddMaterial(string fileName, string contentType, string objectKey, long sizeBytes)
    {
        var material = SessionMaterial.Create(Id, fileName, contentType, objectKey, sizeBytes);
        _materials.Add(material);
        return material;
    }

    public SessionMaterial RemoveMaterial(Guid materialId)
    {
        var material = _materials.FirstOrDefault(m => m.Id == materialId)
            ?? throw new InvalidOperationException($"Material '{materialId}' is not part of this session.");
        _materials.Remove(material);
        return material;
    }

    private SessionVideo FindVideo(Guid videoId)
        => _videos.FirstOrDefault(v => v.Id == videoId)
           ?? throw new InvalidOperationException($"Video '{videoId}' is not part of this session.");

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidatePrice(decimal price)
    {
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
    }

    private static void ValidateValidityDays(int validityDays)
    {
        if (validityDays is < 0 or > MaxValidityDays)
            throw new ArgumentOutOfRangeException(
                nameof(validityDays), $"Validity days must be between 0 and {MaxValidityDays}.");
    }
}
