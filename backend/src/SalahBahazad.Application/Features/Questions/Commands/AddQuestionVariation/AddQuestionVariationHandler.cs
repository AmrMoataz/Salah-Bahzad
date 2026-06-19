using Mediator;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Questions.Commands.AddQuestionVariation;

internal sealed class AddQuestionVariationHandler(
    IAppDbContext db, IFileStorage fileStorage, IAuditWriter auditWriter)
    : IRequestHandler<AddQuestionVariationCommand, QuestionVariationDto>
{
    public async ValueTask<QuestionVariationDto> Handle(
        AddQuestionVariationCommand command, CancellationToken cancellationToken)
    {
        var question = await QuestionLoader.LoadTrackedAsync(db, command.SessionId, command.QuestionId, cancellationToken)
            ?? throw new NotFoundException("Question", command.QuestionId);

        // Optional inline image (allows an image-only variation, FR-PLAT-QB-002). Upload to R2 first so its
        // key is set atomically when the variation is created.
        string? imageObjectKey = null;
        if (!string.IsNullOrWhiteSpace(command.ImageBase64))
        {
            var bytes = Convert.FromBase64String(command.ImageBase64);
            var contentType = command.ImageContentType ?? "application/octet-stream";
            imageObjectKey = StorageKeys.QuestionImage(question.TenantId, contentType);
            using var stream = new MemoryStream(bytes);
            await fileStorage.UploadPrivateAsync(imageObjectKey, stream, contentType, cancellationToken);
        }

        var drafts = command.Options.Select(o => new QuestionOptionDraft(o.Text, o.IsCorrect)).ToList();
        var variation = question.AddVariation(command.BodyLatex, drafts, imageObjectKey);
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest(
                "QuestionVariationAdded", "Session", command.SessionId, "Question variation added."),
            cancellationToken);

        return await variation.ToDtoAsync(fileStorage, cancellationToken);
    }
}
