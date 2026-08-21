using F1Ticketing.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace F1Ticketing.Tests;

public class TicketServiceTests
{
    [Fact]
    public async Task Purchase_applies_early_discount_and_returns_codes()
    {
        await using var db = CreateDb(10);
        var service = CreateService(db);
        var result = await service.PurchaseAsync(Request(), CancellationToken.None);
        Assert.Equal(90m, result.TotalPrice);
        Assert.NotEmpty(result.RegistrationCode);
        Assert.NotEmpty(result.PromoCode);
    }

    [Fact]
    public async Task Purchase_writes_ticket_event_to_outbox()
    {
        await using var db = CreateDb(10);
        var service = new TicketService(
            db,
            new FixedRate(),
            new OutboxWriter(db),
            NullLogger<TicketService>.Instance
        );

        var ticket = await service.PurchaseAsync(Request(), CancellationToken.None);
        var message = await db.OutboxMessages.SingleAsync();

        Assert.Equal("TicketCreated", message.EventName);
        Assert.Null(message.PublishedAt);
        Assert.Contains(ticket.Id.ToString(), message.Payload);
    }

    [Fact]
    public async Task Purchase_rejects_mismatched_email()
    {
        await using var db = CreateDb(10);
        var request = Request() with
        {
            Customer = Request().Customer with { EmailConfirmation = "other@example.com" },
        };
        await Assert.ThrowsAsync<RuleException>(() =>
            CreateService(db).PurchaseAsync(request, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Purchase_rejects_full_zone()
    {
        await using var db = CreateDb(0);
        await Assert.ThrowsAsync<RuleException>(() =>
            CreateService(db).PurchaseAsync(Request(), CancellationToken.None)
        );
    }

    [Fact]
    public async Task Cancel_invalidates_ticket_and_promo_code()
    {
        await using var db = CreateDb(10);
        var ticket = await CreateService(db).PurchaseAsync(Request(), CancellationToken.None);
        await CreateService(db)
            .CancelAsync(ticket.RegistrationCode, ticket.Email, CancellationToken.None);
        var stored = await db.Tickets.SingleAsync();
        Assert.Equal(TicketStatus.Cancelled, stored.Status);
        Assert.StartsWith("CANCELLED", stored.PromoCode);
    }

    [Fact]
    public async Task Paddock_requires_active_race_ticket()
    {
        await using var db = CreateDb(10);
        await Assert.ThrowsAsync<RuleException>(() =>
            CreateService(db)
                .PurchasePaddockAsync(new("missing", false, false, false), CancellationToken.None)
        );
    }

    private static TicketService CreateService(TicketDbContext db) =>
        new(db, new FixedRate(), new NoOutbox(), NullLogger<TicketService>.Instance);

    private static TicketDbContext CreateDb(int capacity)
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new TicketDbContext(options);
        db.Races.Add(
            new Race
            {
                DiscountUntil = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
                RaceDays =
                [
                    new RaceDay
                    {
                        Id = 1,
                        Date = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                        BasePrice = 100,
                        Capacity = capacity,
                    },
                ],
                SeatingZones =
                [
                    new SeatingZone
                    {
                        Id = 1,
                        Name = "Grandstand",
                        Capacity = capacity,
                        Surcharge = 0,
                    },
                ],
            }
        );
        db.Currencies.Add(new Currency { Code = "EUR" });
        db.SaveChanges();
        return db;
    }

    private static PurchaseTicketRequest Request() =>
        new(
            new("A", "B", "Street", "1000", "City", "Country", "a@example.com", "a@example.com"),
            [new(DateOnly.FromDateTime(DateTime.UtcNow.Date), "Grandstand")],
            "EUR",
            null
        );

    private sealed class FixedRate : IExchangeRateService
    {
        public Task<decimal> GetRateAsync(
            string from,
            string to,
            CancellationToken cancellationToken
        ) => Task.FromResult(1m);
    }

    private sealed class NoOutbox : IOutboxWriter
    {
        public void Add(string eventName, object payload) { }
    }
}
