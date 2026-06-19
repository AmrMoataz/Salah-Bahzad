using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.UpdateSessionVideo;

internal sealed class UpdateSessionVideoHandler(
    IAppDbContext db,
    IFileStorage fileStorage,
    IVideoProcessingQueue videoQueue,
    ICurrentUserResolver currentUser,
    IAuditWriter auditWriter)
    : IRequestHandler<UpdateSessionVideoCommand, SessionVideoDto>
{
    public async ValueTask<SessionVideoDto> Handle(
        UpdateSessionVideoCommand command, CancellationToken cancellationToken)
    {
        var session = await db.Sessions
            .Include(s => s.Videos)
            .FirstOrDefaultAsync(s => s.Id == command.SessionId, cancellationToken)
            ?? throw new NotFoundException("Session", command.SessionId);

        if (session.Videos.All(v => v.Id != command.VideoId))
            throw new NotFoundException("Video", command.VideoId);

        string? newObjectKey = null;
        if (command.HasNewSource)
        {
            newObjectKey = StorageKeys.SessionVideo(currentUser.TenantId, command.ContentType!);
            await fileStorage.UploadPrivateAsync(
                newObjectKey, command.Content!, command.ContentType!, cancellationToken);
        }

        var video = session.UpdateVideo(
            command.VideoId, command.Title, command.LengthMinutes, command.AccessCount, newObjectKey);
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest("SessionVideoUpdated", "Session", session.Id, $"Video updated: {video.Title}"),
            cancellationToken);

        var dto = video.ToDto();
        if (newObjectKey is not null)
            await videoQueue.EnqueueTranscodeAsync(video.Id, newObjectKey, cancellationToken);

        return dto;
    }
}
