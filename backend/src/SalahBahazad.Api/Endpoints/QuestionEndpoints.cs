using Mediator;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Api.Authorization;
using SalahBahazad.Application.Common.Models;
using SalahBahazad.Application.Features.Questions.Commands.AddQuestionVariation;
using SalahBahazad.Application.Features.Questions.Commands.ClearQuestionImage;
using SalahBahazad.Application.Features.Questions.Commands.CreateQuestion;
using SalahBahazad.Application.Features.Questions.Commands.DeleteQuestion;
using SalahBahazad.Application.Features.Questions.Commands.RemoveQuestionVariation;
using SalahBahazad.Application.Features.Questions.Commands.SetQuestionImage;
using SalahBahazad.Application.Features.Questions.Commands.SetVariationImage;
using SalahBahazad.Application.Features.Questions.Commands.UpdateQuestion;
using SalahBahazad.Application.Features.Questions.Commands.UpdateQuestionVariation;
using SalahBahazad.Application.Features.Questions.DTOs;
using SalahBahazad.Application.Features.Questions.Queries.GetQuestionById;
using SalahBahazad.Application.Features.Questions.Queries.ListQuestions;
using SalahBahazad.Domain.Enums;

namespace SalahBahazad.Api.Endpoints;

/// <summary>
/// Question-bank endpoints (FR-ADM-QB-001..006, contract rows #19–27) under
/// <c>/api/sessions/{id}/questions</c>. Granular permissions, server-enforced via <c>RequirePermission</c>
/// (default-deny). Image sub-routes upload to R2; read models embed short-lived signed image URLs.
/// </summary>
internal sealed class QuestionEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions/{sessionId:guid}/questions")
            .WithTags("Question bank")
            .WithOpenApi();

        group.MapGet("/", ListAsync)
            .RequirePermission(Permission.QuestionsRead)
            .WithName("ListQuestions")
            .Produces<PagedResult<QuestionDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/{questionId:guid}", GetByIdAsync)
            .RequirePermission(Permission.QuestionsRead)
            .WithName("GetQuestion")
            .Produces<QuestionDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateAsync)
            .RequirePermission(Permission.QuestionsCreate)
            .WithName("CreateQuestion")
            .Produces<QuestionDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/{questionId:guid}", UpdateAsync)
            .RequirePermission(Permission.QuestionsEdit)
            .WithName("UpdateQuestion")
            .Produces<QuestionDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/{questionId:guid}/image", SetImageAsync)
            .RequirePermission(Permission.QuestionsEdit)
            .DisableAntiforgery()
            .WithName("SetQuestionImage")
            .WithSummary("Upload/replace the question image (multipart)")
            .Produces<QuestionDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapDelete("/{questionId:guid}/image", ClearImageAsync)
            .RequirePermission(Permission.QuestionsEdit)
            .WithName("ClearQuestionImage")
            .WithSummary("Remove the question image")
            .Produces<QuestionDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapDelete("/{questionId:guid}", DeleteAsync)
            .RequirePermission(Permission.QuestionsDelete)
            .WithName("DeleteQuestion")
            .WithSummary("Soft-delete (detach) a question")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // ── Variations ────────────────────────────────────────────────────────────
        group.MapPost("/{questionId:guid}/variations", AddVariationAsync)
            .RequirePermission(Permission.QuestionsEdit)
            .WithName("AddQuestionVariation")
            .Produces<QuestionVariationDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/{questionId:guid}/variations/{variationId:guid}", UpdateVariationAsync)
            .RequirePermission(Permission.QuestionsEdit)
            .WithName("UpdateQuestionVariation")
            .Produces<QuestionVariationDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/{questionId:guid}/variations/{variationId:guid}/image", SetVariationImageAsync)
            .RequirePermission(Permission.QuestionsEdit)
            .DisableAntiforgery()
            .WithName("SetQuestionVariationImage")
            .WithSummary("Upload/replace a variation image (multipart)")
            .Produces<QuestionVariationDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapDelete("/{questionId:guid}/variations/{variationId:guid}", RemoveVariationAsync)
            .RequirePermission(Permission.QuestionsEdit)
            .WithName("RemoveQuestionVariation")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListAsync(
        Guid sessionId,
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Results.Ok(await sender.Send(new ListQuestionsQuery(sessionId, page, pageSize), cancellationToken));

    private static async Task<IResult> GetByIdAsync(
        Guid sessionId, Guid questionId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetQuestionByIdQuery(sessionId, questionId), cancellationToken));

    private static async Task<IResult> CreateAsync(
        Guid sessionId, [FromBody] SaveQuestionRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateQuestionCommand(
                sessionId, request.BodyLatex, request.Mark, request.IsValidForQuiz, request.HintUrl,
                request.ToOptionInputs(), request.ImageBase64, request.ImageContentType),
            cancellationToken);
        return Results.Created($"/api/sessions/{sessionId}/questions/{result.Id}", result);
    }

    private static async Task<IResult> UpdateAsync(
        Guid sessionId,
        Guid questionId,
        [FromBody] SaveQuestionRequest request,
        ISender sender,
        CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(
            new UpdateQuestionCommand(
                sessionId, questionId, request.BodyLatex, request.Mark, request.IsValidForQuiz, request.HintUrl,
                request.ToOptionInputs()),
            cancellationToken));

    private static async Task<IResult> SetImageAsync(
        Guid sessionId, Guid questionId, IFormFile file, ISender sender, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        return Results.Ok(await sender.Send(
            new SetQuestionImageCommand(sessionId, questionId, content, file.ContentType, file.Length, file.FileName),
            cancellationToken));
    }

    private static async Task<IResult> ClearImageAsync(
        Guid sessionId, Guid questionId, ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new ClearQuestionImageCommand(sessionId, questionId), cancellationToken));

    private static async Task<IResult> DeleteAsync(
        Guid sessionId, Guid questionId, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteQuestionCommand(sessionId, questionId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AddVariationAsync(
        Guid sessionId,
        Guid questionId,
        [FromBody] SaveVariationRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AddQuestionVariationCommand(
                sessionId, questionId, request.BodyLatex, request.ToOptionInputs(),
                request.ImageBase64, request.ImageContentType),
            cancellationToken);
        return Results.Created(
            $"/api/sessions/{sessionId}/questions/{questionId}/variations/{result.Id}", result);
    }

    private static async Task<IResult> UpdateVariationAsync(
        Guid sessionId,
        Guid questionId,
        Guid variationId,
        [FromBody] SaveVariationRequest request,
        ISender sender,
        CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(
            new UpdateQuestionVariationCommand(
                sessionId, questionId, variationId, request.BodyLatex, request.ToOptionInputs()),
            cancellationToken));

    private static async Task<IResult> SetVariationImageAsync(
        Guid sessionId,
        Guid questionId,
        Guid variationId,
        IFormFile file,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        return Results.Ok(await sender.Send(
            new SetVariationImageCommand(
                sessionId, questionId, variationId, content, file.ContentType, file.Length, file.FileName),
            cancellationToken));
    }

    private static async Task<IResult> RemoveVariationAsync(
        Guid sessionId, Guid questionId, Guid variationId, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveQuestionVariationCommand(sessionId, questionId, variationId), cancellationToken);
        return Results.NoContent();
    }
}

/// <summary>An MCQ option in a request body; the client may echo an id on update (ignored — Phase 3
/// reassigns option identity server-side).</summary>
internal sealed record OptionRequest(string Text, bool IsCorrect, Guid? Id = null);

/// <summary>Request body for create/update question (#20 / #21). On create, an image may be supplied
/// inline as base64 (<paramref name="ImageBase64"/> + <paramref name="ImageContentType"/>) so an
/// image-only question can be created in one call; it can still be replaced later via the image endpoint.</summary>
internal sealed record SaveQuestionRequest(
    string? BodyLatex,
    int Mark,
    bool IsValidForQuiz,
    string? HintUrl,
    IReadOnlyList<OptionRequest>? Options,
    string? ImageBase64 = null,
    string? ImageContentType = null)
{
    public IReadOnlyList<QuestionOptionInput> ToOptionInputs()
        => (Options ?? []).Select(o => new QuestionOptionInput(o.Text, o.IsCorrect)).ToList();
}

/// <summary>Request body for create/update variation (#24 / #25). On add, an image may be supplied inline
/// as base64 (so an image-only variation can be created in one call); ignored on update.</summary>
internal sealed record SaveVariationRequest(
    string? BodyLatex,
    IReadOnlyList<OptionRequest>? Options,
    string? ImageBase64 = null,
    string? ImageContentType = null)
{
    public IReadOnlyList<QuestionOptionInput> ToOptionInputs()
        => (Options ?? []).Select(o => new QuestionOptionInput(o.Text, o.IsCorrect)).ToList();
}
