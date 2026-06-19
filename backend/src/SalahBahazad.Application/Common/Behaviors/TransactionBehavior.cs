using Mediator;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Common.Behaviors;

/// <summary>
/// Wraps <see cref="ITransactionalRequest"/> commands in a single database transaction so multi-write
/// handlers commit atomically and domain events dispatch only after the commit (backend/CLAUDE.md —
/// "Pipeline behaviours used for: validation, transaction scope, and audit context injection").
/// Requests that don't opt in (queries, single-write commands) pass straight through.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IAppDbContext db)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    public async ValueTask<TResponse> Handle(
        TRequest message,
        CancellationToken cancellationToken,
        MessageHandlerDelegate<TRequest, TResponse> next)
    {
        if (message is not ITransactionalRequest)
            return await next(message, cancellationToken);

        return await db.ExecuteInTransactionAsync(
            () => next(message, cancellationToken).AsTask(),
            cancellationToken);
    }
}
