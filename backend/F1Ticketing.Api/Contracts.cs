namespace F1Ticketing.Api;

public record RaceRequest(
    string Name,
    string Location,
    DateOnly StartDate,
    DateOnly EndDate,
    string? AdditionalInformation,
    DateOnly DiscountUntil,
    List<RaceDayRequest> RaceDays,
    List<ZoneRequest> SeatingZones,
    List<string>? AllowedCurrencies
);

public record RaceDayRequest(DateOnly Date, decimal BasePrice, int Capacity);

public record ZoneRequest(string Name, string Characteristics, int Capacity, decimal Surcharge);

public record CustomerRequest(
    string FirstName,
    string LastName,
    string Address1,
    string PostalCode,
    string City,
    string Country,
    string Email,
    string EmailConfirmation
);

public record TicketDayRequest(DateOnly Date, string Zone);

public record PurchaseTicketRequest(
    CustomerRequest Customer,
    List<TicketDayRequest> Days,
    string Currency,
    string? PromoCode
);

public record ModifyTicketRequest(
    string RegistrationCode,
    string Email,
    List<TicketDayRequest> AddDays,
    List<DateOnly> RemoveDays,
    string Currency
);

public record PaddockRequest(string RegistrationCode, bool PitLane, bool Food, bool Drinks);

public record TicketResponse(
    Guid Id,
    string RegistrationCode,
    string PromoCode,
    decimal TotalPrice,
    string Currency,
    object Customer,
    object Days
);

public record PaddockResponse(
    Guid Id,
    decimal TotalPrice,
    string Currency,
    bool PitLane,
    bool Food,
    bool Drinks
);
