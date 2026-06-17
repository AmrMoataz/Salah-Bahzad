using Mediator;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace SalahBahazad.Application.Common.Behaviors;

/// <summary>Logs the start/end of every command/query and its wall-clock duration.</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    public async ValueTask<TResponse> Handle(
        TRequest message,
        CancellationToken cancellationToken,
        MessageHandlerDelegate<TRequest, TResponse> next)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        logger.LogInformation("→ {Request}", name);
        try
        {
            var response = await next(message, cancellationToken);
            logger.LogInformation("← {Request} ({Elapsed}ms)", name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "✗ {Request} failed ({Elapsed}ms)", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
