namespace F1Ticketing.Api;

public class SeatingZone
{
    // Zona definiše karakteristike, kapacitet i doplatu na osnovnu cenu.
    public int Id { get; set; }
    public int RaceId { get; set; }
    public Race Race { get; set; } = null!;
    public string Name { get; set; } = "General Admission";
    public string Characteristics { get; set; } = "";
    public int Capacity { get; set; }
    public decimal Surcharge { get; set; }
}
