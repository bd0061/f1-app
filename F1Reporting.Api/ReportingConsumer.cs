using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace F1Reporting.Api;

public sealed class ReportingConsumer(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<ReportingConsumer> logger
) : BackgroundService
{
    // BackgroundService pokreće ExecuteAsync kada ASP.NET Core podigne
    // aplikaciju. Ovaj servis tada ostaje aktivan dok aplikacija ne stane.
    private readonly ReportingEventProcessor processor = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting reporting RabbitMQ consumer");
        var factory = new ConnectionFactory
        {
            Uri = new Uri(
                configuration["RabbitMq:ConnectionString"] ?? "amqp://f1:f1@localhost:5672/"
            ),
        };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        // A.1 objavljuje na fanout exchange-u, a ova aplikacija ima svoju
        // trajnu queue instancu da ne bi delila poruke sa drugim potrošačem.
        channel.ExchangeDeclare("f1.ticket-events", ExchangeType.Fanout, durable: true);
        channel.QueueDeclare("f1.reporting", durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind("f1.reporting", "f1.ticket-events", "");
        logger.LogInformation(
            "Reporting consumer bound queue {Queue} to exchange {Exchange}",
            "f1.reporting",
            "f1.ticket-events"
        );
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (_, args) =>
            ProcessAsync(channel, args, stoppingToken).GetAwaiter().GetResult();
        // Ručna potvrda znači da poruku potvrđujemo tek nakon uspešnog upisa
        // u reporting bazu.
        channel.BasicConsume("f1.reporting", autoAck: false, consumer);
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }
    }

    private async Task ProcessAsync(
        IModel channel,
        BasicDeliverEventArgs args,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
            await processor.ProcessAsync(db, args.Body.ToArray(), cancellationToken);
            channel.BasicAck(args.DeliveryTag, false);
            logger.LogDebug("Acknowledged reporting message {DeliveryTag}", args.DeliveryTag);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not process reporting event");
            channel.BasicNack(args.DeliveryTag, false, requeue: false);
            logger.LogWarning(
                "Rejected reporting message {DeliveryTag} without requeue",
                args.DeliveryTag
            );
        }
    }
}
