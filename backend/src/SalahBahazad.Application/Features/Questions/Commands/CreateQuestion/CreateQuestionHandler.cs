using Mediator;
using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Questions.DTOs;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Features.Questions.Commands.CreateQuestion;

internal sealed class CreateQuestionHandler(
    IAppDbContext db, IFileStorage fileStorage, ICurrentUserResolver currentUser, IAuditWriter auditWriter)
    : IRequestHandler<CreateQuestionCommand, QuestionDto>
{
    public async ValueTask<QuestionDto> Handle(CreateQuestionCommand command, CancellationToken cancellationToken)
    {
        // The session must exist (and be live) in the caller's tenant (query filter applies).
        if (!await db.Sessions.AnyAsync(s => s.Id == command.SessionId, cancellationToken))
            throw new NotFoundException("Session", command.SessionId);

        // Optional inline image (allows an image-only question, FR-PLAT-QB-002). Upload to R2 first so its
        // key is set atomically when the question is created — no window where a question has no content.
        string? imageObjectKey = null;
        if (!string.IsNullOrWhiteSpace(command.ImageBase64))
        {
            var bytes = Convert.FromBase64String(command.ImageBase64);
            var contentType = command.ImageContentType ?? "application/octet-stream";
            imageObjectKey = StorageKeys.QuestionImage(currentUser.TenantId, contentType);
            using var stream = new MemoryStream(bytes);
            await fileStorage.UploadPrivateAsync(imageObjectKey, stream, contentType, cancellationToken);
        }

        var drafts = command.Options.Select(o => new QuestionOptionDraft(o.Text, o.IsCorrect)).ToList();
        var question = Question.Create(
            currentUser.TenantId,
            command.SessionId,
            command.BodyLatex,
            command.Mark,
            command.IsValidForQuiz,
            command.HintUrl,
            drafts,
            imageObjectKey);

        db.Questions.Add(question);
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest("QuestionAdded", "Session", command.SessionId, "Question added to the bank."),
            cancellationToken);

        return await question.ToDtoAsync(fileStorage, cancellationToken);
    }
}
