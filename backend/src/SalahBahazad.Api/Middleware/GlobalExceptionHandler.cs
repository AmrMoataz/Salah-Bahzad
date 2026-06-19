using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SalahBahazad.Application.Common.Exceptions;

namespace SalahBahazad.Api.Middleware;

/// <summary>
/// Translates application/domain exceptions into RFC 7807 ProblemDetails with the correct HTTP status
/// (closes the gap where <c>UseExceptionHandler()</c> previously returned plain 500s, issue #12).
/// 5xx details are never leaked to the client (NFR-OBS-001).
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, errors) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception processing {Path}", httpContext.Request.Path);
        else
            logger.LogInformation(
                "Request {Path} failed: {ExceptionType} → {Status}",
                httpContext.Request.Path, exception.GetType().Name, status);

        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : exception.Message,
        };

        if (errors is not null)
            problemDetails.Extensions["errors"] = errors;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    private static (int Status, string Title, IDictionary<string, string[]>? Errors) Map(Exception exception) =>
        exception switch
        {
            ValidationException ve => (
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                ve.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray())),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found.", null),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict.", null),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden.", null),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized.", null),
            // Malformed multipart / over-size source uploads (streaming cap, bad boundary) — client errors.
            InvalidDataException => (StatusCodes.Status400BadRequest, "Malformed or oversized request.", null),
            BadHttpRequestException bad => (bad.StatusCode, "Bad request.", null),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null),
        };
}
