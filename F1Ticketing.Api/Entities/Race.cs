namespace F1Ticketing.Api;

public class Race
{
    // Jedan zapis predstavlja trku čije podatke organizator podešava.
    public int Id { get; set; } = 1;
    public string Name { get; set; } = "F1 Grand Prix";
    public string Location { get; set; } = "TBA";
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date);
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2));
    public string AdditionalInformation { get; set; } = "";
    public DateOnly DiscountUntil { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date);

    // Trka ima više dana, zona sedišta i termina za koje su dostupne karte.
    public List<RaceDay> RaceDays { get; set; } = [];
    public List<SeatingZone> SeatingZones { get; set; } = [];
}
