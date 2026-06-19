using Mediator;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.SetQuestionImage;

internal sealed class SetQuestionImageHandler(
    IAppDbContext db, IFileStorage fileStorage, ICurrentUserResolver currentUser, IAuditWriter auditWriter)
    : IRequestHandler<SetQuestionImageCommand, QuestionDto>
{
    public async ValueTask<QuestionDto> Handle(SetQuestionImageCommand command, CancellationToken cancellationToken)
    {
        var question = await QuestionLoader.LoadTrackedAsync(db, command.SessionId, command.QuestionId, cancellationToken)
            ?? throw new NotFoundException("Question", command.QuestionId);

        var objectKey = StorageKeys.QuestionImage(currentUser.TenantId, command.ContentType);
        await fileStorage.UploadPrivateAsync(objectKey, command.Content, command.ContentType, cancellationToken);

        question.SetImage(objectKey);
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest("QuestionImageUpdated", "Session", command.SessionId, "Question image updated."),
            cancellationToken);

        return await question.ToDtoAsync(fileStorage, cancellationToken);
    }
}
