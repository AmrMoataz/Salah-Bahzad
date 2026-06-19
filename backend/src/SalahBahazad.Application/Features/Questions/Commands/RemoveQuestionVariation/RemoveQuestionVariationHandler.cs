using Mediator;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Questions.Commands.RemoveQuestionVariation;

internal sealed class RemoveQuestionVariationHandler(IAppDbContext db, IAuditWriter auditWriter)
    : IRequestHandler<RemoveQuestionVariationCommand, Unit>
{
    public async ValueTask<Unit> Handle(RemoveQuestionVariationCommand command, CancellationToken cancellationToken)
    {
        var question = await QuestionLoader.LoadTrackedAsync(db, command.SessionId, command.QuestionId, cancellationToken)
            ?? throw new NotFoundException("Question", command.QuestionId);

        if (question.Variations.All(v => v.Id != command.VariationId))
            throw new NotFoundException("Variation", command.VariationId);

        question.RemoveVariation(command.VariationId);
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                "QuestionVariationRemoved", "Session", command.SessionId, "Question variation removed."),
            cancellationToken);

        return Unit.Value;
    }
}
