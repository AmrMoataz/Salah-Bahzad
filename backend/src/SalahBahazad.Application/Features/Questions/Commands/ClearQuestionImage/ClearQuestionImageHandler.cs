using Mediator;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;

namespace SalahBahazad.Application.Features.Questions.Commands.ClearQuestionImage;

internal sealed class ClearQuestionImageHandler(IAppDbContext db, IFileStorage fileStorage, IAuditWriter auditWriter)
    : IRequestHandler<ClearQuestionImageCommand, QuestionDto>
{
    public async ValueTask<QuestionDto> Handle(ClearQuestionImageCommand command, CancellationToken cancellationToken)
    {
        var question = await QuestionLoader.LoadTrackedAsync(db, command.SessionId, command.QuestionId, cancellationToken)
            ?? throw new NotFoundException("Question", command.QuestionId);

        try
        {
            question.ClearImage();
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest("QuestionImageRemoved", "Session", command.SessionId, "Question image removed."),
            cancellationToken);

        return await question.ToDtoAsync(fileStorage, cancellationToken);
    }
}
