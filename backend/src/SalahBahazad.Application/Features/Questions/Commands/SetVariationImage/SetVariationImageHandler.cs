using Mediator;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.SetVariationImage;

internal sealed class SetVariationImageHandler(
    IAppDbContext db, IFileStorage fileStorage, ICurrentUserResolver currentUser, IAuditWriter auditWriter)
    : IRequestHandler<SetVariationImageCommand, QuestionVariationDto>
{
    public async ValueTask<QuestionVariationDto> Handle(
        SetVariationImageCommand command, CancellationToken cancellationToken)
    {
        var question = await QuestionLoader.LoadTrackedAsync(db, command.SessionId, command.QuestionId, cancellationToken)
            ?? throw new NotFoundException("Question", command.QuestionId);

        var variation = question.Variations.FirstOrDefault(v => v.Id == command.VariationId)
            ?? throw new NotFoundException("Variation", command.VariationId);

        var objectKey = StorageKeys.QuestionImage(currentUser.TenantId, command.ContentType);
        await fileStorage.UploadPrivateAsync(objectKey, command.Content, command.ContentType, cancellationToken);

        question.SetVariationImage(command.VariationId, objectKey);
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                "QuestionVariationImageUpdated", "Session", command.SessionId, "Question variation image updated."),
            cancellationToken);

        return await variation.ToDtoAsync(fileStorage, cancellationToken);
    }
}
