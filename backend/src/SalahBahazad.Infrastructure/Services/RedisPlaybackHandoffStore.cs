using Microsoft.Extensions.DependencyInjection;
using SalahBahazad.Application.Common.Interfaces;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text.Json;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// <see cref="IPlaybackHandoffStore"/> over Redis: the one-time handoff code is a random key holding the
/// serialized <see cref="PlaybackHandoff"/> with a short TTL. Consumption uses Redis <c>GETDEL</c> so a code
/// can be redeemed at most once even across instances (FR-PLAT-VID-005). Redis is resolved lazily and required —
/// the playback gate only runs with the full stack (dev/integration both have Redis).
/// </summary>
internal sealed class RedisPlaybackHandoffStore(IServiceProvider serviceProvider) : IPlaybackHandoffStore
{
    private const string KeyPrefix = "playback:handoff:";

    public async Task<string> IssueAsync(
        PlaybackHandoff handoff, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var code = GenerateCode();
        var json = JsonSerializer.Serialize(handoff);
        await GetRedis().StringSetAsync(KeyPrefix + code, json, ttl);
        return code;
    }

    public async Task<PlaybackHandoff?> ConsumeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var value = await GetRedis().StringGetDeleteAsync(KeyPrefix + code); // atomic single-use
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<PlaybackHandoff>(value.ToString());
    }

    private IDatabase GetRedis()
        => (serviceProvider.GetService<IConnectionMultiplexer>()
            ?? throw new InvalidOperationException(
                "Redis is required for video playback handoff codes but is not configured (ConnectionStrings__redis)."))
            .GetDatabase();

    private static string GenerateCode()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}
