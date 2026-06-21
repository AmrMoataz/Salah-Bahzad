using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Application.Features.Videos.Queries.GetHlsKey;

/// <summary>
/// Serves the AES-128 key after re-checking authorisation (active+unexpired enrollment, quiz passed if gated;
/// tenant/IDOR via the session → 404). No decrement, no audit (it follows an already-audited gate and is
/// re-fetched often). Streams the bytes from the private key object.
/// </summary>
internal sealed class GetHlsKeyHandler(
    IAppDbContext db,
    ICurrentUserResolver currentUser,
    IFileStorage fileStorage,
    TimeProvider clock)
    : IRequestHandler<GetHlsKeyQuery, byte[]>
{
    public async ValueTask<byte[]> Handle(GetHlsKeyQuery query, CancellationToken cancellationToken)
    {
        var video = await db.Sessions
            .AsNoTracking()
            .SelectMany(s => s.Videos)
            .Where(v => v.Id == query.VideoId)
            .Select(v => new { v.SessionId, v.HlsKeyObjectKey, v.ProcessingStatus })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Video", query.VideoId);

        if (video.ProcessingStatus != VideoProcessingStatus.Ready || string.IsNullOrEmpty(video.HlsKeyObjectKey))
            throw new ConflictException("This video is still being processed.", "not_ready");

        var enrollment = await db.Enrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.SessionId == video.SessionId && e.StudentId == currentUser.UserId, cancellationToken);

        if (enrollment is null || enrollment.Status != EnrollmentStatus.Active)
            throw new ForbiddenException("You are not enrolled in this session.", "not_enrolled");

        if (enrollment.ExpiresAtUtc is { } expiresAt && expiresAt <= clock.GetUtcNow())
            throw new ForbiddenException("Your enrollment for this session has expired.", "enrollment_expired");

        var quiz = await db.UserQuizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.EnrollmentId == enrollment.Id, cancellationToken);
        if (quiz is not null && !quiz.Passed)
            throw new ForbiddenException("Pass the prerequisite quiz to unlock this video.", "quiz_required");

        await using var keyStream = await fileStorage.OpenReadAsync(video.HlsKeyObjectKey, cancellationToken);
        using var buffer = new MemoryStream();
        await keyStream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
