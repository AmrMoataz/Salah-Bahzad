using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Codes.DTOs;

namespace SalahBahazad.Application.Features.Codes.Commands.GenerateCodeBatch;

/// <summary>
/// Mints a batch of redemption codes for a session (FR-PLAT-COD-001). <see cref="Value"/> defaults to the
/// session's current price when omitted (the Generate modal pre-fills it; contract §5). Transactional so the
/// batch and its codes — and the single batch-generated audit entry — commit together.
/// </summary>
public sealed record GenerateCodeBatchCommand(Guid SessionId, decimal? Value, int Quantity)
    : IRequest<CodeBatchDto>, ITransactionalRequest;
