using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace F1Ticketing.Api.Services;

public sealed class RedisRaceCache(IConfiguration configuration, ILogger<RedisRaceCache> logger)
    : IRaceCache
{
    private readonly Lazy<ConnectionMultiplexer?> connection = new(() =>
    {
        var value = configuration.GetConnectionString("Redis");
        return string.IsNullOrWhiteSpace(value) ? null : ConnectionMultiplexer.Connect(value);
    });

    // Podaci trke se često čitaju, zato serijalizovani odgovor držimo u
    // Redis-u deset minuta.
    public async Task<string?> GetAsync()
    {
        if (connection.Value is null)
            return null;
        var value = await connection.Value.GetDatabase().StringGetAsync("f1:race");
        logger.LogDebug("Redis keš trke: {CacheStatus}", value.HasValue ? "pogodak" : "promašaj");
        return value.HasValue ? value.ToString() : null;
    }

    //10min ttl
    public async Task SetAsync(string value)
    {
        if (connection.Value is null)
            return;
        await connection
            .Value.GetDatabase()
            .StringSetAsync("f1:race", value, TimeSpan.FromMinutes(10));
        logger.LogDebug("Podaci trke su upisani u Redis na deset minuta");
    }

    public async Task InvalidateAsync()
    {
        if (connection.Value is null)
            return;

        await connection.Value.GetDatabase().KeyDeleteAsync("f1:race");
        logger.LogDebug("Redis keš trke je invalidiran");
    }
}
