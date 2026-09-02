using System.Text.Json;
using F1Ticketing.Api;
using F1Ticketing.Api.Exceptions;
using F1Ticketing.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// PostgreSQL je trajno spremište. Redis služi kao keš, a RabbitMQ kao kanal
// za događaje koje koristi Reporting Portal.
builder.Services.AddDbContext<TicketDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);
builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>();
builder.Services.AddSingleton<IEventPublisher, RabbitEventPublisher>();
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
builder.Services.AddScoped<OutboxDispatcher>();
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddSingleton<IRaceCache, RedisRaceCache>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.Logger.LogInformation("Starting F1 ticketing API");

// Pri pokretanju se primenjuju EF Core migracije. Ako je baza prazna, ubacuje
// se kompletan primer trke kako bi svi glavni tokovi mogli odmah da se probaju.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
    app.Logger.LogInformation("Applying database migrations");
    db.Database.Migrate();

    if (!db.Races.Any())
    {
        db.Races.Add(
            new Race
            {
                Name = "Monaco Grand Prix",
                Location = "Circuit de Monaco, Monte Carlo",
                StartDate = new DateOnly(2027, 5, 21),
                EndDate = new DateOnly(2027, 5, 23),
                DiscountUntil = new DateOnly(2027, 2, 28),
                AdditionalInformation =
                    "Historic Formula 1 race through the streets of Monte Carlo. "
                    + "The weekend includes practice, qualifying and the main race.",
                RaceDays =
                [
                    new RaceDay
                    {
                        Date = new DateOnly(2027, 5, 21),
                        BasePrice = 180,
                        Capacity = 20000,
                    },
                    new RaceDay
                    {
                        Date = new DateOnly(2027, 5, 22),
                        BasePrice = 220,
                        Capacity = 20000,
                    },
                    new RaceDay
                    {
                        Date = new DateOnly(2027, 5, 23),
                        BasePrice = 300,
                        Capacity = 20000,
                    },
                ],
                SeatingZones =
                [
                    new SeatingZone
                    {
                        Name = "Casino Grandstand",
                        Characteristics = "Covered grandstand with views of Casino Square",
                        Capacity = 5000,
                        Surcharge = 120,
                    },
                    new SeatingZone
                    {
                        Name = "Harbour Grandstand",
                        Characteristics = "Open grandstand overlooking the harbour section",
                        Capacity = 6000,
                        Surcharge = 80,
                    },
                    new SeatingZone
                    {
                        Name = "General Admission",
                        Characteristics = "Unreserved standing areas around the circuit",
                        Capacity = 8000,
                        Surcharge = 0,
                    },
                ],
            }
        );
    }

    // Valute su globalne i seeduju se odvojeno od trke. Na taj način se
    // katalog može popraviti i ako baza već ima race zapis.
    if (!db.Currencies.Any())
    {
        db.Currencies.AddRange(
            new Currency { Code = "EUR" },
            new Currency { Code = "USD" },
            new Currency { Code = "GBP" },
            new Currency { Code = "CHF" }
        );
    }

    db.SaveChanges();
    app.Logger.LogInformation("Seeded sample race and global currency catalog when required");
}
app.UseSwagger();
app.UseSwaggerUI();

// Javne informacije o trci prvo se čitaju iz Redis keša. Ako ih nema, čitaju
// se iz PostgreSQL baze i zatim upisuju u Redis na deset minuta.
app.MapGet(                    
    "/api/race",
    async (TicketDbContext db, IRaceCache cache) =>
    {
        app.Logger.LogInformation("Reading race information");
        var cached = await cache.GetAsync();
        if (cached is not null)
            return Results.Content(cached, "application/json");
        var race = await db
            .Races.Include(x => x.RaceDays)
            .Include(x => x.SeatingZones)
            .SingleAsync();
        var json = JsonSerializer.Serialize(
            ToRaceResponse(race, await db.Currencies.Where(x => x.IsAllowed).ToListAsync())
        );
        await cache.SetAsync(json);
        return Results.Content(json, "application/json");
    }
);
app.MapGet(
    "/api/currencies",
    async (TicketDbContext db, CancellationToken ct) =>
        await db
            .Currencies.Where(x => x.IsAllowed)
            .OrderBy(x => x.Code)
            .Select(x => x.Code)
            .ToListAsync(ct)
);

// Administratori ovde menjaju podatke trke, dane, zone i globalne valute.
app.MapPut(
    "/api/admin/race",
    async (RaceRequest request, TicketDbContext db, IRaceCache cache) =>
    {
        app.Logger.LogInformation(
            "Updating race configuration with {RaceDayCount} race days",
            request.RaceDays.Count
        );
        var current = await db
            .Races.Include(x => x.RaceDays)
            .Include(x => x.SeatingZones)
            .SingleAsync();
        current.Name = request.Name;
        current.Location = request.Location;
        current.StartDate = request.StartDate;
        current.EndDate = request.EndDate;
        current.AdditionalInformation = request.AdditionalInformation ?? "";
        current.DiscountUntil = request.DiscountUntil;
        if (request.AllowedCurrencies is null || request.AllowedCurrencies.Count == 0)
            return Results.BadRequest(new { error = "At least one allowed currency is required." });
        else
        {
            var requestedCodes = request
                .AllowedCurrencies.Select(x => x.Trim().ToUpperInvariant())
                .Where(x => x.Length > 0)
                .Distinct()
                .ToHashSet();
            if (requestedCodes.Count == 0)
                return Results.BadRequest(
                    new { error = "At least one allowed currency is required." }
                );
            var currencies = await db.Currencies.ToListAsync();
            foreach (var currency in currencies)
                currency.IsAllowed = requestedCodes.Contains(currency.Code);
            db.Currencies.AddRange(
                requestedCodes
                    .Where(code => currencies.All(x => x.Code != code))
                    .Select(code => new Currency { Code = code, IsAllowed = true })
            );
        }
        db.RaceDays.RemoveRange(current.RaceDays);
        db.SeatingZones.RemoveRange(current.SeatingZones);
        current.RaceDays = request
            .RaceDays.Select(x => new RaceDay
            {
                Date = x.Date,
                BasePrice = x.BasePrice,
                Capacity = x.Capacity,
            })
            .ToList();
        current.SeatingZones = request
            .SeatingZones.Select(x => new SeatingZone
            {
                Name = x.Name,
                Characteristics = x.Characteristics,
                Capacity = x.Capacity,
                Surcharge = x.Surcharge,
            })
            .ToList();
        await db.SaveChangesAsync();
        await cache.InvalidateAsync();
        var response = ToRaceResponse(
            current,
            await db.Currencies.Where(x => x.IsAllowed).ToListAsync()
        );
        await cache.SetAsync(JsonSerializer.Serialize(response));
        return Results.Ok(response);
    }
);

// TicketService objedinjuje validaciju, obračun, čuvanje i RabbitMQ događaje.
app.MapPost(
    "/api/tickets",
    async (PurchaseTicketRequest request, TicketService service, CancellationToken ct) =>
    {
        try
        {
            return Results.Ok(ToResponse(await service.PurchaseAsync(request, ct)));
        }
        catch (RuleException e)
        {
            return Results.BadRequest(new { error = e.Message });
        }
    }
);
app.MapPut(
    "/api/tickets",
    async (ModifyTicketRequest request, TicketService service, CancellationToken ct) =>
    {
        try
        {
            return Results.Ok(ToResponse(await service.ModifyAsync(request, ct)));
        }
        catch (RuleException e)
        {
            return Results.BadRequest(new { error = e.Message });
        }
    }
);
app.MapPost(
    "/api/tickets/cancel",
    async (string registrationCode, string email, TicketService service, CancellationToken ct) =>
    {
        try
        {
            await service.CancelAsync(registrationCode, email, ct);
            return Results.NoContent();
        }
        catch (RuleException e)
        {
            return Results.BadRequest(new { error = e.Message });
        }
    }
);
app.MapPost(
    "/api/paddock-passes",
    async (PaddockRequest request, TicketService service, CancellationToken ct) =>
    {
        try
        {
            return Results.Ok(ToPaddockResponse(await service.PurchasePaddockAsync(request, ct)));
        }
        catch (RuleException e)
        {
            return Results.BadRequest(new { error = e.Message });
        }
    }
);
app.Run();

static TicketResponse ToResponse(Ticket ticket) =>
    new(
        ticket.Id,
        ticket.RegistrationCode,
        ticket.PromoCode,
        ticket.TotalPrice,
        ticket.Currency.Code,
        new
        {
            ticket.FirstName,
            ticket.LastName,
            ticket.Address1,
            ticket.PostalCode,
            ticket.City,
            ticket.Country,
            ticket.Email,
        },
        ticket.Days.Select(x => new
        {
            x.RaceDayId,
            x.SeatingZoneId,
            x.Price,
        })
    );

static PaddockResponse ToPaddockResponse(PaddockPass pass) =>
    new(
        pass.Id,
        pass.TotalPrice,
        pass.Ticket.Currency.Code,
        pass.PitLane,
        pass.Food,
        pass.Drinks
    );

static object ToRaceResponse(Race race, IEnumerable<Currency> currencies) =>
    new
    {
        race.Id,
        race.Name,
        race.Location,
        race.StartDate,
        race.EndDate,
        race.AdditionalInformation,
        race.DiscountUntil,
        AllowedCurrencies = currencies.Select(x => x.Code).OrderBy(x => x),
        RaceDays = race.RaceDays.Select(x => new
        {
            x.Id,
            x.RaceId,
            x.Date,
            x.BasePrice,
            x.Capacity,
        }),
        SeatingZones = race.SeatingZones.Select(x => new
        {
            x.Id,
            x.RaceId,
            x.Name,
            x.Characteristics,
            x.Capacity,
            x.Surcharge,
        }),
    };

public partial class Program { }
