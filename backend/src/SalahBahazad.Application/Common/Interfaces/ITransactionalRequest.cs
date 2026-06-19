namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Marker for commands that must run inside a single database transaction. The
/// <c>TransactionBehavior</c> wraps these so multi-write handlers commit atomically, and
/// buffered domain events are dispatched only after the transaction commits
/// (backend/CLAUDE.md — CQRS pipeline behaviours: "transaction scope"). Queries and
/// single-write commands need not implement it.
/// </summary>
public interface ITransactionalRequest;
