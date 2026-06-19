using FluentValidation;
using FluentValidation.Results;
using Mediator;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Questions.Commands.UpdateQuestionVariation;

internal sealed class UpdateQuestionVariationHandler(
    IAppDbContext db, IFileStorage fileStorage, IAuditWriter auditWriter)
    : IRequestHandler<UpdateQuestionVariationCommand, QuestionVariationDto>
{
    public async ValueTask<QuestionVariationDto> Handle(
        UpdateQuestionVariationCommand command, CancellationToken cancellationToken)
    {
        var question = await QuestionLoader.LoadTrackedAsync(db, command.SessionId, command.QuestionId, cancellationToken)
            ?? throw new NotFoundException("Question", command.QuestionId);

        var variation = question.Variations.FirstOrDefault(v => v.Id == command.VariationId)
            ?? throw new NotFoundException("Variation", command.VariationId);

        // Image-aware body invariant (the validator can't see the stored image) → 400.
        if (string.IsNullOrWhiteSpace(command.BodyLatex) && string.IsNullOrWhiteSpace(variation.ImageObjectKey))
            throw new ValidationException(
                [new ValidationFailure(nameof(command.BodyLatex), "LaTeX text and/or an image is required.")]);

        var drafts = command.Options.Select(o => new QuestionOptionDraft(o.Text, o.IsCorrect)).ToList();
        question.UpdateVariation(command.VariationId, command.BodyLatex, drafts);
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                "QuestionVariationUpdated", "Session", command.SessionId, "Question variation updated."),
            cancellationToken);

        return await variation.ToDtoAsync(fileStorage, cancellationToken);
    }
}
