using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace F1Ticketing.Api;

public interface IOutboxWriter
{
    // Samo dodaje događaj u trenutni EF Core DbContext. Ne šalje ništa
    // RabbitMQ-u i ne izvršava SaveChanges; pozivalac to radi zajedno sa
    // poslovnim promenama, u istoj transakciji.
    void Add(string eventName, object payload);
}

public sealed class OutboxWriter(TicketDbContext db) : IOutboxWriter
{
    public void Add(string eventName, object payload)
    {
        // Payload čuvamo kao JSON zato što outbox tabela mora moći da sačuva
        // različite tipove događaja bez posebne kolone za svaki tip.
        db.OutboxMessages.Add(
            new OutboxMessage { EventName = eventName, Payload = JsonSerializer.Serialize(payload) }
        );
    }
}

public sealed class OutboxDispatcher(
    TicketDbContext db,
    IEventPublisher publisher,
    ILogger<OutboxDispatcher> logger
)
{
    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken)
    {
        // U jednom prolazu obrađujemo najviše 50 neposlatih poruka. Time jedan
        // prolaz ne zaključava ili ne učitava neograničenu količinu podataka.
        var messages = await db
            .OutboxMessages.Where(x => x.PublishedAt == null)
            .OrderBy(x => x.OccurredAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                // Ponovo pretvaramo JSON tekst u objekat koji publisher stavlja
                // u standardni RabbitMQ event envelope.
                using var payload = JsonDocument.Parse(message.Payload);
                await publisher.PublishAsync(
                    message.EventName,
                    payload.RootElement,
                    cancellationToken
                );
                message.PublishedAt = DateTimeOffset.UtcNow;
                message.LastError = null;
                // PublishedAt je dokaz da je publisher završio bez greške.
                // Sledeći prolaz više neće izabrati ovu poruku.
                logger.LogInformation(
                    "Published outbox message {MessageId} as {EventName}",
                    message.Id,
                    message.EventName
                );
            }
            catch (Exception exception)
            {
                // Poruka ostaje sa PublishedAt == null. Sledeći prolaz će je
                // ponovo pokušati, a Attempts i LastError pomažu dijagnostici.
                message.Attempts++;
                message.LastError = exception.Message;
                logger.LogWarning(
                    exception,
                    "Could not publish outbox message {MessageId}",
                    message.Id
                );
            }
        }

        // Statusi uspešnih i neuspešnih pokušaja čuvaju se tek nakon obrade
        // celog batch-a. Sam događaj ostaje u bazi dok publish ne uspe.
        await db.SaveChangesAsync(cancellationToken);
        return messages.Count(x => x.PublishedAt is not null);
    }
}

public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopes,
    ILogger<OutboxPublisherWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ASP.NET Core automatski poziva ExecuteAsync kada se aplikacija
        // pokrene, zato worker ne pozivamo ručno iz endpoint-a.
        logger.LogInformation("Starting outbox publisher worker");
        while (!stoppingToken.IsCancellationRequested)
        {
            // Novi scope daje dispatcher-u svež DbContext po ciklusu i sprečava
            // da dugotrajni background proces koristi zastareo tracking state.
            using var scope = scopes.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
            await dispatcher.PublishPendingAsync(stoppingToken);
            // Ako nema poruka ili je broker privremeno nedostupan, čekamo dva
            // sekunda pre sledećeg pokušaja. Pending redovi se ne gube.
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
