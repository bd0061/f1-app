namespace F1Ticketing.Api;

public enum TicketStatus
{
    Active,
    Cancelled,
}

public class Ticket
{
    // Karta čuva kupca, ukupnu cenu i status. Otkazana karta se ne briše.
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RegistrationCode { get; set; } = "";
    public string PromoCode { get; set; } = "";
    public bool PromoCodeUsed { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Active;
    public DateTimeOffset PurchasedAt { get; set; } = DateTimeOffset.UtcNow;
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Address1 { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
    public string Email { get; set; } = "";
    public int CurrencyId { get; set; }
    public Currency Currency { get; set; } = null!;
    public decimal TotalPrice { get; set; }

    // Jedna karta može obuhvatiti više dana; detalji su u TicketDay tabeli.
    public List<TicketDay> Days { get; set; } = [];
    public PaddockPass? PaddockPass { get; set; }
}

public class TicketDay
{
    // Ovo je spojna tabela između karte, dana trke i zone sedišta.
    public int Id { get; set; }
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public int RaceDayId { get; set; }
    public RaceDay RaceDay { get; set; } = null!;
    public int SeatingZoneId { get; set; }
    public SeatingZone SeatingZone { get; set; } = null!;
    public decimal Price { get; set; }
}
