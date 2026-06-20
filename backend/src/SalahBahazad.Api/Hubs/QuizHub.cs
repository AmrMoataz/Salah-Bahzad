using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SalahBahazad.Application.Common.Interfaces;
using StackExchange.Redis;

namespace SalahBahazad.Api.Hubs;

/// <summary>
/// The proctored-quiz hub (contract §A, path <c>/hubs/quiz</c>). Authenticated with the <b>same platform JWT</b>
/// as the REST engine — the token rides the SignalR <c>access_token</c> query, scoped to this path and validated
/// as a full JWT by <c>JwtBearerEvents.OnMessageReceived</c> (NFR-SEC-005; issue #6 done right). Student-only:
/// a non-student principal is aborted. Its core job is <b>single-sitting forfeit</b> (FR-PLAT-QZ-004): when the
/// connection drops, the caller's active attempt is forfeited with score 0. The connection↔attempt mapping is
/// kept in Redis (so it holds across instances); a DB lookup is the fallback when the map is missing.
/// </summary>
[Authorize]
public sealed class QuizHub(
    IQuizLifecycleService lifecycle, IServiceProvider serviceProvider, ILogger<QuizHub> logger) : Hub
{
    private static string ConnectionKey(string connectionId) => $"quizhub:conn:{connectionId}";
    private static string QuizGroup(Guid quizId) => $"quiz:{quizId:N}";

    public override async Task OnConnectedAsync()
    {
        // Proctoring is a student act; reject any other principal (defence in depth atop RequireStudent semantics).
        if (!TryGetStudent(out var studentId, out var tenantId))
        {
            Context.Abort();
            return;
        }

        // If an attempt is already active (started via REST before opening the socket), bind this connection to it.
        var quizId = await lifecycle.FindActiveAttemptQuizIdAsync(studentId, tenantId, Context.ConnectionAborted);
        if (quizId is Guid id)
        {
            await StoreMappingAsync(Context.ConnectionId, id);
            await Groups.AddToGroupAsync(Context.ConnectionId, QuizGroup(id));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Single-sitting forfeit (FR-PLAT-QZ-004): a lost connection forfeits the active attempt with score 0.
        if (TryGetStudent(out var studentId, out var tenantId))
        {
            // Prefer the Redis mapping; fall back to the DB so any connect/start ordering is covered.
            var quizId = await ReadMappingAsync(Context.ConnectionId)
                ?? await lifecycle.FindActiveAttemptQuizIdAsync(studentId, tenantId);

            if (quizId is Guid id)
            {
                await lifecycle.ForfeitActiveAttemptAsync(id, tenantId);
                await RemoveMappingAsync(Context.ConnectionId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private bool TryGetStudent(out Guid studentId, out Guid tenantId)
    {
        studentId = Guid.Empty;
        tenantId = Guid.Empty;

        var user = Context.User;
        if (user is null)
            return false;

        var role = user.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase))
            return false;

        return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out studentId)
            && Guid.TryParse(user.FindFirstValue("tenant_id"), out tenantId);
    }

    // ── Redis connection↔attempt map (best-effort; the DB fallback keeps forfeit correct without it) ────
    private IDatabase? Redis()
    {
        try
        {
            return serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis unavailable for the quiz hub connection map.");
            return null;
        }
    }

    private async Task StoreMappingAsync(string connectionId, Guid quizId)
    {
        if (Redis() is { } db)
            await db.StringSetAsync(ConnectionKey(connectionId), quizId.ToString("N"), TimeSpan.FromHours(6));
    }

    private async Task<Guid?> ReadMappingAsync(string connectionId)
    {
        if (Redis() is not { } db)
            return null;
        var value = await db.StringGetAsync(ConnectionKey(connectionId));
        return value.HasValue && Guid.TryParseExact(value!, "N", out var id) ? id : null;
    }

    private async Task RemoveMappingAsync(string connectionId)
    {
        if (Redis() is { } db)
            await db.KeyDeleteAsync(ConnectionKey(connectionId));
    }
}
