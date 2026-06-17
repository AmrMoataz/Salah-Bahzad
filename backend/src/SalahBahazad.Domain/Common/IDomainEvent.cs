using Mediator;

namespace SalahBahazad.Domain.Common;

/// <summary>
/// Domain event raised by aggregates and dispatched by the Infrastructure layer
/// <b>after</b> a successful SaveChanges — handlers only see committed state.
/// Extends INotification so handlers can be INotificationHandler&lt;T&gt;.
/// </summary>
public interface IDomainEvent : INotification
{
    DateTimeOffset OccurredAtUtc { get; }
}
