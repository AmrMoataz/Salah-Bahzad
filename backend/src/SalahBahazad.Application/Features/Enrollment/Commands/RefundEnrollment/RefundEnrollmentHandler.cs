using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Enrollment.DTOs;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Enrollment.Commands.RefundEnrollment;

internal sealed class RefundEnrollmentHandler(
    IAppDbContext db, TimeProvider clock, ICurrentUserResolver currentUser, ILogger<RefundEnrollmentHandler> logger)
    : IRequestHandler<RefundEnrollmentCommand, EnrollmentDto>
{
    public async ValueTask<EnrollmentDto> Handle(RefundEnrollmentCommand command, CancellationToken cancellationToken)
    {
        var enrollment = await db.Enrollments
            .FirstOrDefaultAsync(e => e.Id == command.EnrollmentId, cancellationToken)
            ?? throw new NotFoundException("Enrollment", command.EnrollmentId);

        if (enrollment.Status != EnrollmentStatus.Active)
            throw new ConflictException("Only an active enrollment can be refunded.");

        // Return the redeemed code to circulation (Used → Active) so it can be redeemed again (FR-PLAT-ENR-008).
        string? returnedCodeSerial = null;
        if (enrollment.Method == EnrollmentMethod.Code && enrollment.CodeId is Guid codeId)
        {
            var code = await db.Codes.FirstOrDefaultAsync(c => c.Id == codeId, cancellationToken);
            if (code is not null)
            {
                code.ReturnAfterRefund();
                returnedCodeSerial = code.Serial;
            }
        }

        enrollment.Refund(clock.GetUtcNow(), returnedCodeSerial);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Enrollment {EnrollmentId} refunded by {ActorId}", enrollment.Id, currentUser.UserId);

        return await EnrollmentLoader.LoadDtoAsync(db, enrollment.Id, cancellationToken)
            ?? throw new NotFoundException("Enrollment", enrollment.Id);
    }
}
