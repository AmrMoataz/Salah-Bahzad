using Mediator;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;

namespace SalahBahazad.Application.Features.Questions.Commands.DeleteQuestion;

internal sealed class DeleteQuestionHandler(
    IAppDbContext db,
    TimeProvider clock,
    ICurrentUserResolver currentUser,
    IAuditWriter auditWriter,
    ILogger<DeleteQuestionHandler> logger)
    : IRequestHandler<DeleteQuestionCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteQuestionCommand command, CancellationToken cancellationToken)
    {
        var question = await QuestionLoader.LoadTrackedAsync(db, command.SessionId, command.QuestionId, cancellationToken)
            ?? throw new NotFoundException("Question", command.QuestionId);

        question.SoftDelete(currentUser.UserId, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                "QuestionDetached", "Session", command.SessionId, "Question detached from the bank."),
            cancellationToken);

        logger.LogInformation("Question {QuestionId} soft-deleted by {ActorId}", question.Id, currentUser.UserId);
        return Unit.Value;
    }
}
