using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Commands.ClearStudentDevice;

internal sealed class ClearStudentDeviceHandler(
    IAppDbContext db,
    ICurrentUserResolver currentUser,
    TimeProvider clock)
    : IRequestHandler<ClearStudentDeviceCommand, StudentDetailDto>
{
    public async ValueTask<StudentDetailDto> Handle(
        ClearStudentDeviceCommand command, CancellationToken cancellationToken)
    {
        var studentExists = await db.Students.AnyAsync(s => s.Id == command.StudentId, cancellationToken);
        if (!studentExists)
            throw new NotFoundException("Student", command.StudentId);

        var device = await db.StudentDevices
            .FirstOrDefaultAsync(d => d.StudentId == command.StudentId && d.IsActive, cancellationToken)
            ?? throw new ConflictException("This student has no active device to clear.");

        // The StudentDeviceClearedEvent carries the reason into the audit Summary (FR-PLAT-DEV-004).
        device.Clear(currentUser.UserId, command.Reason, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return await StudentDetailLoader.LoadAsync(db, command.StudentId, cancellationToken)
            ?? throw new NotFoundException("Student", command.StudentId);
    }
}
