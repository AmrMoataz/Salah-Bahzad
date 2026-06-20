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
        // Validate the target exists (tenant-filtered IDOR gate) before any expensive upload, so replacing
        // the source of a bad/forbidden video id never streams an orphan into R2.
        var videoExists = await db.Sessions
            .Where(s => s.Id == command.SessionId)
            .SelectMany(s => s.Videos)
            .AnyAsync(v => v.Id == command.VideoId, cancellationToken);
        if (!videoExists)
            throw new NotFoundException("Video", command.VideoId);

        // Stream the replacement source to R2 BEFORE opening a transaction (see AddSessionVideoHandler).
        string? newObjectKey = null;
        if (command.HasNewSource)
        {
            newObjectKey = StorageKeys.SessionVideo(currentUser.TenantId, command.SessionId, command.ContentType!);
            await fileStorage.UploadPrivateStreamingAsync(
                newObjectKey, command.Content!, command.ContentType!, cancellationToken);
        }

        return await db.ExecuteInTransactionAsync(async () =>
        {
            var session = await db.Sessions
                .Include(s => s.Videos)
                .FirstOrDefaultAsync(s => s.Id == command.SessionId, cancellationToken)
                ?? throw new NotFoundException("Session", command.SessionId);

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
        }, cancellationToken);
    }
}
