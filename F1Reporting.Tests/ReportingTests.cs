using System.Text.Json;
using F1Reporting.Api;
using Microsoft.EntityFrameworkCore;

namespace F1Reporting.Tests;

public class ReportingTests
{
    [Fact]
    public async Task Tickets_by_race_day_counts_each_ticket_once_per_day()
    {
        await using var db = CreateDb();
        var first = new ReportedTicket { Id = Guid.NewGuid(), PurchasedAt = DateTimeOffset.UtcNow };
        first.Days.Add(new ReportedTicketDay { RaceDayDate = new DateOnly(2026, 9, 1) });
        first.Days.Add(new ReportedTicketDay { RaceDayDate = new DateOnly(2026, 9, 2) });
        var second = new ReportedTicket
        {
            Id = Guid.NewGuid(),
            PurchasedAt = DateTimeOffset.UtcNow,
        };
        second.Days.Add(new ReportedTicketDay { RaceDayDate = new DateOnly(2026, 9, 1) });
        db.Tickets.AddRange(first, second);
        await db.SaveChangesAsync();

        var result = await new ReportingQueries(db).TicketsByRaceDayAsync(CancellationToken.None);

        Assert.Equal(2, result.Single(x => x.RaceDay == new DateOnly(2026, 9, 1)).Tickets);
        Assert.Equal(1, result.Single(x => x.RaceDay == new DateOnly(2026, 9, 2)).Tickets);
    }

    [Fact]
    public async Task Purchases_by_date_counts_tickets_and_honors_start_date()
    {
        await using var db = CreateDb();
        db.Tickets.AddRange(
            new ReportedTicket
            {
                Id = Guid.NewGuid(),
                PurchasedAt = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
            },
            new ReportedTicket
            {
                Id = Guid.NewGuid(),
                PurchasedAt = new DateTimeOffset(2026, 8, 2, 10, 0, 0, TimeSpan.Zero),
            },
            new ReportedTicket
            {
                Id = Guid.NewGuid(),
                PurchasedAt = new DateTimeOffset(2026, 8, 2, 11, 0, 0, TimeSpan.Zero),
            }
        );
        await db.SaveChangesAsync();

        var result = await new ReportingQueries(db).PurchasesByDateAsync(
            new DateOnly(2026, 8, 2),
            CancellationToken.None
        );

        Assert.Single(result);
        Assert.Equal(2, result[0].Purchases);
    }

    [Fact]
    public async Task Paddock_report_counts_passes_and_each_option()
    {
        await using var db = CreateDb();
        db.PaddockPasses.AddRange(
            new ReportedPaddockPass
            {
                Id = Guid.NewGuid(),
                TicketId = Guid.NewGuid(),
                PitLane = true,
                Food = true,
            },
            new ReportedPaddockPass
            {
                Id = Guid.NewGuid(),
                TicketId = Guid.NewGuid(),
                PitLane = true,
                Drinks = true,
            }
        );
        await db.SaveChangesAsync();

        var result = await new ReportingQueries(db).PaddockOptionsAsync(CancellationToken.None);

        Assert.Equal(new PaddockOptionCount(2, 2, 1, 1), result);
    }

    [Fact]
    public async Task Event_processor_stores_ticket_days_and_is_idempotent()
    {
        await using var db = CreateDb();
        var id = Guid.NewGuid();
        var body = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                EventName = "TicketCreated",
                Payload = new
                {
                    Id = id,
                    PurchasedAt = DateTimeOffset.UtcNow,
                    RaceDays = new[]
                    {
                        new { Date = new DateOnly(2026, 9, 1) },
                        new { Date = new DateOnly(2026, 9, 2) },
                    },
                },
            }
        );
        var processor = new ReportingEventProcessor();

        await processor.ProcessAsync(db, body);
        await processor.ProcessAsync(db, body);

        Assert.Single(db.Tickets);
        Assert.Equal(2, db.TicketDays.Count());
    }

    [Fact]
    public async Task Event_processor_stores_paddock_options()
    {
        await using var db = CreateDb();
        var id = Guid.NewGuid();
        var body = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                EventName = "PaddockPassCreated",
                Payload = new
                {
                    Id = id,
                    TicketId = Guid.NewGuid(),
                    PitLane = true,
                    Food = false,
                    Drinks = true,
                },
            }
        );

        await new ReportingEventProcessor().ProcessAsync(db, body);

        var pass = await db.PaddockPasses.SingleAsync();
        Assert.True(pass.PitLane);
        Assert.False(pass.Food);
        Assert.True(pass.Drinks);
    }

    private static ReportingDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReportingDbContext(options);
    }
}
