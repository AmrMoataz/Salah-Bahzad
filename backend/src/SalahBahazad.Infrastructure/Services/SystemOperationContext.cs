using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// <see cref="ISystemOperationContext"/> backed by an <see cref="AsyncLocal{T}"/> so the ambient tenant flows
/// down the async call chain of a background job or hub callback and is restored when the scope disposes.
/// Registered as a singleton; the <c>AsyncLocal</c> keeps each logical operation isolated.
/// </summary>
internal sealed class SystemOperationContext : ISystemOperationContext
{
    private static readonly AsyncLocal<SystemOperation?> Ambient = new();

    public SystemOperation? Current => Ambient.Value;

    public IDisposable Begin(Guid tenantId)
    {
        var previous = Ambient.Value;
        Ambient.Value = new SystemOperation(tenantId);
        return new Scope(previous);
    }

    private sealed class Scope(SystemOperation? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Ambient.Value = previous;
        }
    }
}
