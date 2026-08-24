namespace F1Ticketing.Api;

public class RaceDay
{
    // Dan trke ima osnovnu cenu i ukupan kapacitet.
    public int Id { get; set; }
    public int RaceId { get; set; }
    public Race Race { get; set; } = null!;
    public DateOnly Date { get; set; }
    public decimal BasePrice { get; set; }
    public int Capacity { get; set; }
}
