using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Staff.Commands.SendStaffPasswordReset;

internal sealed class SendStaffPasswordResetHandler(
    IAppDbContext db,
    IFirebaseAuthService firebaseAuth,
    ICurrentUserResolver currentUser,
    ILogger<SendStaffPasswordResetHandler> logger)
    : IRequestHandler<SendStaffPasswordResetCommand, StaffPasswordResetResponse>
{
    public async ValueTask<StaffPasswordResetResponse> Handle(
        SendStaffPasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        var staff = await db.Staff.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Staff member", command.Id);

        await firebaseAuth.SendPasswordResetEmailAsync(staff.Email, cancellationToken);

        logger.LogInformation(
            "Password-reset email requested for staff {StaffId} by {ActorId}", staff.Id, currentUser.UserId);

        return new StaffPasswordResetResponse(staff.Email);
    }
}
