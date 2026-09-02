using System.Text.Json;
using RabbitMQ.Client;

namespace F1Ticketing.Api.Services;

public sealed class RabbitEventPublisher(
    IConfiguration configuration,
    ILogger<RabbitEventPublisher> logger
) : IEventPublisher
{
    public Task PublishAsync(
        string eventName,
        object payload,
        CancellationToken cancellationToken = default
    )
    {
        var connectionString = configuration["RabbitMq:ConnectionString"];
        // Lokalno se može raditi i bez RabbitMQ-a. U normalnom radu konekcija
        // dolazi iz konfiguracije.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("RabbitMQ is not configured; skipping {EventName} event", eventName);
            return Task.CompletedTask;
        }
        try
        {
            logger.LogDebug("Povezivanje sa RabbitMQ radi objave događaja {EventName}", eventName);
            var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.ExchangeDeclare("f1.ticket-events", ExchangeType.Fanout, durable: true);
            var body = JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    EventName = eventName,
                    Payload = payload,
                    OccurredAt = DateTimeOffset.UtcNow,
                }
            );
            channel.BasicPublish("f1.ticket-events", "", null, body);
            logger.LogInformation(
                "Objavljen je događaj {EventName} na RabbitMQ exchange-u {Exchange}",
                eventName,
                "f1.ticket-events"
            );
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Događaj {EventName} nije mogao biti objavljen",
                eventName
            );
            // Dispatcher mora videti grešku da bi outbox zapis ostao pending
            // i mogao biti ponovo pokušan pri sledećem prolazu.
            throw;
        }
        return Task.CompletedTask;
    }
}
