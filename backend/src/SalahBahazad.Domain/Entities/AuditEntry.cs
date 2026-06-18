namespace SalahBahazad.Domain.Entities;

/// <summary>
/// Append-only, immutable audit record (FR-PLAT-AUD-001/003, NFR-AUD-001/003).
/// Written atomically with the action by the SaveChangesInterceptor and explicit event handlers.
/// Never update or delete this entity through application code.
/// </summary>
public sealed class AuditEntry
{
    private AuditEntry() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid TenantId { get; private set; }

    // Who
    public Guid? ActorId { get; private set; }
    public string? ActorRole { get; private set; }
    public string ActorType { get; private set; } = string.Empty; // "Staff" | "Student" | "System"

    // What
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public string? Summary { get; private set; }
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }

    // Where
    public string? IpAddress { get; private set; }
    public string? Portal { get; private set; }   // "admin" | "student" | "system"
    public string? DeviceId { get; private set; }

    // When
    public DateTimeOffset OccurredAtUtc { get; private set; }

    // Tamper-evidence (NFR-AUD-001)
    public string? PrevHash { get; private set; }
    public string? Hash { get; private set; }

    public static AuditEntry Create(
        Guid tenantId,
        string action,
        string entityType,
        DateTimeOffset occurredAtUtc,
        Guid? entityId = null,
        Guid? actorId = null,
        string? actorRole = null,
        string actorType = "System",
        string? summary = null,
        string? beforeJson = null,
        string? afterJson = null,
        string? ipAddress = null,
        string? portal = null,
        string? deviceId = null,
        string? prevHash = null)
    {
        return new AuditEntry
        {
            TenantId = tenantId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ActorId = actorId,
            ActorRole = actorRole,
            ActorType = actorType,
            Summary = summary,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            IpAddress = ipAddress,
            Portal = portal,
            DeviceId = deviceId,
            OccurredAtUtc = occurredAtUtc,
            PrevHash = prevHash,
        };
    }

    internal void SetHash(string hash) => Hash = hash;
}
