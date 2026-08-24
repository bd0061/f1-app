namespace F1Ticketing.Api;

// Outbox zapis čuva događaj zajedno sa poslovnim podacima u istoj bazi.
// Publisher ga kasnije šalje u RabbitMQ, pa privremeni kvar brokera ne gubi događaj.
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventName { get; set; } = "";
    public string Payload { get; set; } = "{}";
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
