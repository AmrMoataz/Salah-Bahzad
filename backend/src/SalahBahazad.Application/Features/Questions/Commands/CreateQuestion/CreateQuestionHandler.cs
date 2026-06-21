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

        // Optional inline image (allows an image-only question, FR-PLAT-QB-002). The key embeds the question id,
        // so we pre-generate the id and upload to R2 *before* Create — passing the key in so the body-or-image
        // invariant is satisfied atomically for an image-only question (an upload-after-Create would throw).
        var questionId = Guid.CreateVersion7();
        string? imageObjectKey = null;
        if (!string.IsNullOrWhiteSpace(command.ImageBase64))
        {
            var bytes = Convert.FromBase64String(command.ImageBase64);
            var contentType = command.ImageContentType ?? "application/octet-stream";
            imageObjectKey = StorageKeys.QuestionImage(
                currentUser.TenantId, command.SessionId, questionId, contentType);
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
            imageObjectKey,
            questionId);

        db.Questions.Add(question);
        await db.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            new AuditWriteRequest("QuestionAdded", "Session", command.SessionId, "Question added to the bank."),
            cancellationToken);

        return await question.ToDtoAsync(fileStorage, cancellationToken);
    }
}
