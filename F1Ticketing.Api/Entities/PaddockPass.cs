namespace F1Ticketing.Api;

public class PaddockPass
{
    // Pass pripada postojećoj karti i može postojati najviše jedan po karti.
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public DateTimeOffset PurchasedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool PitLane { get; set; }
    public bool Food { get; set; }
    public bool Drinks { get; set; }
    public decimal TotalPrice { get; set; }
}
