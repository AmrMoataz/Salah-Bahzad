using Mediator;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Enrollment.DTOs;

namespace SalahBahazad.Application.Features.Enrollment.Commands.RefundEnrollment;

/// <summary>
/// Staff refund an active enrollment (#10, FR-PLAT-ENR-008): status → Refunded, a reversing payment is
/// written, and when it was granted by a code that code is returned to circulation (Used → Active).
/// Transactional so the enrollment, payment and code-return commit together.
/// </summary>
public sealed record RefundEnrollmentCommand(Guid EnrollmentId, string? Reason)
    : IRequest<EnrollmentDto>, ITransactionalRequest;
