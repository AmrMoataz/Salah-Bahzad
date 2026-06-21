using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Sessions.DTOs;

namespace SalahBahazad.Application.Features.Sessions.Commands.AddSessionVideo;

internal sealed class AddSessionVideoHandler(
    IAppDbContext db,
    IFileStorage fileStorage,
    IVideoProcessingQueue videoQueue,
    ICurrentUserResolver currentUser,
    IAuditWriter auditWriter)
    : IRequestHandler<AddSessionVideoCommand, SessionVideoDto>
{
    public async ValueTask<SessionVideoDto> Handle(AddSessionVideoCommand command, CancellationToken cancellationToken)
    {
        // Cheap existence check (tenant-filtered, so also the IDOR gate) before the expensive upload, so a
        // bad/forbidden session id never streams a multi-GB orphan into R2.
        var sessionExists = await db.Sessions.AnyAsync(s => s.Id == command.SessionId, cancellationToken);
        if (!sessionExists)
            throw new NotFoundException("Session", command.SessionId);

        // Stream the source straight to R2 BEFORE opening a transaction — a multi-GB upload must not hold a
        // database transaction (and its connection) open for the duration (docs/05 §3/§6, FR-PLAT-VID-007).
        var objectKey = StorageKeys.SessionVideo(currentUser.TenantId, command.SessionId, command.ContentType);
        await fileStorage.UploadPrivateStreamingAsync(
            objectKey, command.Content, command.ContentType, cancellationToken);

        // The fast DB writes commit atomically (video + audit + stub transcode flip).
        return await db.ExecuteInTransactionAsync(async () =>
        {
            var session = await db.Sessions
                .Include(s => s.Videos)
                .FirstOrDefaultAsync(s => s.Id == command.SessionId, cancellationToken)
                ?? throw new NotFoundException("Session", command.SessionId);

            var video = session.AddVideo(command.Title, command.AccessCount, objectKey);
            await db.SaveChangesAsync(cancellationToken);

            // Session-keyed semantic entry so this shows in the session Activity feed (FR-PLAT-SES-009);
            // the interceptor's per-video row remains as the field-level detail.
            await auditWriter.WriteAsync(
                new AuditWriteRequest("SessionVideoAdded", "Session", session.Id, $"Video added: {video.Title}"),
                cancellationToken);

            // Snapshot the freshly-created (Pending) state for the 201 response, then enqueue transcoding —
            // the stub flips it to Ready, observable on the next read (contract: POST returns Pending/Processing).
            var dto = video.ToDto();
            await videoQueue.EnqueueTranscodeAsync(video.Id, objectKey, cancellationToken);

            return dto;
        }, cancellationToken);
    }
}
