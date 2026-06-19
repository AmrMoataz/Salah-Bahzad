using FluentValidation;
using FluentValidation.Results;
using Mediator;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Questions.Commands.UpdateQuestion;

internal sealed class UpdateQuestionHandler(IAppDbContext db, IFileStorage fileStorage, IAuditWriter auditWriter)
    : IRequestHandler<UpdateQuestionCommand, QuestionDto>
{
    public async ValueTask<QuestionDto> Handle(UpdateQuestionCommand command, CancellationToken cancellationToken)
    {
        var question = await QuestionLoader.LoadTrackedAsync(db, command.SessionId, command.QuestionId, cancellationToken)
            ?? throw new NotFoundException("Question", command.QuestionId);

        // Body invariant is image-aware (the validator can't see the stored image), so check it here → 400.
        if (string.IsNullOrWhiteSpace(command.BodyLatex) && string.IsNullOrWhiteSpace(question.ImageObjectKey))
            throw new ValidationException(
                [new ValidationFailure(nameof(command.BodyLatex), "LaTeX text and/or an image is required.")]);

        var drafts = command.Options.Select(o => new QuestionOptionDraft(o.Text, o.IsCorrect)).ToList();
        question.Update(command.BodyLatex, command.Mark, command.IsValidForQuiz, command.HintUrl, drafts);
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest("QuestionUpdated", "Session", command.SessionId, "Question updated."),
            cancellationToken);

        return await question.ToDtoAsync(fileStorage, cancellationToken);
    }
}
