using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;

namespace SalahBahazad.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that runs FluentValidation validators before the handler.
/// Throws ValidationException (translated to 400 by the exception middleware) on failure.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    public async ValueTask<TResponse> Handle(
        TRequest message,
        CancellationToken cancellationToken,
        MessageHandlerDelegate<TRequest, TResponse> next)
    {
        if (!validators.Any())
            return await next(message, cancellationToken);

        var context = new ValidationContext<TRequest>(message);
        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            logger.LogDebug("Validation failed for {RequestType}: {Errors}",
                typeof(TRequest).Name, failures.Select(f => f.ErrorMessage));
            throw new ValidationException(failures);
        }

        return await next(message, cancellationToken);
    }
}
