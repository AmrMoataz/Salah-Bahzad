namespace SalahBahazad.Domain.Common;

/// <summary>
/// Base type for all persisted entities. Provides a UUIDv7 identity, audit stamps
/// (<see cref="CreatedById"/>/<see cref="CreatedAtUtc"/> and update equivalents), and a
/// domain-event buffer flushed after a successful commit.
/// </summary>
public abstract class EntityBase
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Primary key. UUIDv7 (time-ordered) generated on construction.</summary>
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    /// <summary>Actor that created the row (null for system/seed).</summary>
    public Guid? CreatedById { get; set; }

    /// <summary>Creation timestamp (UTC), stamped by the audit interceptor.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Actor that last updated the row.</summary>
    public Guid? UpdatedById { get; set; }

    /// <summary>Last-update timestamp (UTC); null until first update.</summary>
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    /// <summary>Domain events buffered for dispatch after a successful commit.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Enqueue a domain event to be dispatched after the next successful commit.</summary>
    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Clear the buffer (called by the dispatcher after a commit).</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
