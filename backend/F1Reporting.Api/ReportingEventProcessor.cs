using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace F1Reporting.Api;

public sealed class ReportingEventProcessor
{
    public async Task ProcessAsync(
        ReportingDbContext db,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default
    )
    {
        // Procesor je odvojen od RabbitMQ callback-a da bi se logika mogla
        // testirati bez pokretanja brokera.
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var eventName = root.GetProperty("EventName").GetString();
        var payload = root.GetProperty("Payload");
        // Reporting trenutno koristi samo kreiranje tiketa i paddock pass-a.
        // Izmene i otkazivanja se namerno ignorišu prema zahtevu.
        if (eventName == "TicketCreated")
            await StoreTicketAsync(db, payload, cancellationToken);
        if (eventName == "PaddockPassCreated")
            await StorePaddockPassAsync(db, payload, cancellationToken);
    }

    private static async Task StoreTicketAsync(
        ReportingDbContext db,
        JsonElement payload,
        CancellationToken cancellationToken
    )
    {
        var id = payload.GetProperty("Id").GetGuid();
        if (await db.Tickets.AnyAsync(x => x.Id == id, cancellationToken))
            return;
        var ticket = new ReportedTicket
        {
            Id = id,
            PurchasedAt = payload.GetProperty("PurchasedAt").GetDateTimeOffset(),
        };
        foreach (var day in payload.GetProperty("RaceDays").EnumerateArray())
            ticket.Days.Add(
                new ReportedTicketDay
                {
                    TicketId = id,
                    RaceDayDate = JsonSerializer.Deserialize<DateOnly>(
                        day.GetProperty("Date").GetRawText()
                    ),
                }
            );
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task StorePaddockPassAsync(
        ReportingDbContext db,
        JsonElement payload,
        CancellationToken cancellationToken
    )
    {
        var id = payload.GetProperty("Id").GetGuid();
        if (await db.PaddockPasses.AnyAsync(x => x.Id == id, cancellationToken))
            return;
        db.PaddockPasses.Add(
            new ReportedPaddockPass
            {
                Id = id,
                TicketId = payload.GetProperty("TicketId").GetGuid(),
                PitLane = payload.GetProperty("PitLane").GetBoolean(),
                Food = payload.GetProperty("Food").GetBoolean(),
                Drinks = payload.GetProperty("Drinks").GetBoolean(),
            }
        );
        await db.SaveChangesAsync(cancellationToken);
    }
}
