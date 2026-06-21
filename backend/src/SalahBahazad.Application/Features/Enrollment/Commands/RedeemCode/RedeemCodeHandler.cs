using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Enrollment.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Enrollment.Commands.RedeemCode;

internal sealed class RedeemCodeHandler(
    IAppDbContext db, ICurrentUserResolver currentUser, TimeProvider clock, ILogger<RedeemCodeHandler> logger)
    : IRequestHandler<RedeemCodeCommand, EnrollmentDto>
{
    public async ValueTask<EnrollmentDto> Handle(RedeemCodeCommand command, CancellationToken cancellationToken)
    {
        var studentId = currentUser.UserId;
        var serial = command.Serial.Trim().ToUpperInvariant();

        // Code must exist, be active, and not be soft-deleted (the query filter hides deleted/cross-tenant).
        // A missing/inactive code is a 409, not a 404 (contract §1 redeem invariants).
        var code = await db.Codes.FirstOrDefaultAsync(c => c.Serial == serial, cancellationToken)
            ?? throw new ConflictException("This code is invalid or no longer available.");

        if (code.Status != CodeStatus.Active)
            throw new ConflictException("This code is not available for redemption.");

        var session = await db.Sessions
            .Include(s => s.Videos)
            .FirstOrDefaultAsync(s => s.Id == code.SessionId, cancellationToken)
            ?? throw new ConflictException("The session for this code is no longer available.");

		var student = await db.Students
		   .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken)
		   ?? throw new NotFoundException("Student", studentId);

		// Re-check value == price at redemption so a later price change blocks stale codes (FR-PLAT-COD-003).
		if (code.Value != session.Price)
            throw new ConflictException("This code's value no longer matches the session price.");

        var now = clock.GetUtcNow();

        // Shared cycle of truth (also enforces the one-active-enrollment 409, FR-PLAT-ENR-006).
        var enrollment = await EnrollmentWorkflow.EnrollOrExtendAsync(
            db, currentUser.TenantId, session, studentId, student.FullName,
			EnrollmentMethod.Code, code.Id, code.Value, now, cancellationToken);

        code.MarkRedeemed(studentId, enrollment.Id, now);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Code {Serial} redeemed by student {StudentId}", code.Serial, studentId);

        return await EnrollmentLoader.LoadDtoAsync(db, enrollment.Id, cancellationToken)
            ?? throw new NotFoundException("Enrollment", enrollment.Id);
    }
}
