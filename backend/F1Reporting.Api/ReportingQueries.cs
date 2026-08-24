using Microsoft.EntityFrameworkCore;

namespace F1Reporting.Api;

public sealed class ReportingQueries(ReportingDbContext db)
{
    // Upiti rade nad lokalnom reporting bazom, ne nad bazom za prodaju karata.
    public async Task<List<TicketDayCount>> TicketsByRaceDayAsync(
        CancellationToken cancellationToken
    )
    {
        var days = await db
            .TicketDays.Select(x => new { x.RaceDayDate, x.TicketId })
            .ToListAsync(cancellationToken);
        return days.GroupBy(x => x.RaceDayDate)
            .Select(x => new TicketDayCount(
                x.Key,
                x.Select(day => day.TicketId).Distinct().Count()
            ))
            .OrderBy(x => x.RaceDay)
            .ToList();
    }

    public async Task<List<PurchaseDateCount>> PurchasesByDateAsync(
        DateOnly? from,
        CancellationToken cancellationToken
    )
    {
        var query = db.Tickets.AsQueryable();
        if (from.HasValue)
            query = query.Where(x =>
                x.PurchasedAt >= from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            );
        var purchases = await query.Select(x => x.PurchasedAt).ToListAsync(cancellationToken);
        return purchases
            .GroupBy(x => DateOnly.FromDateTime(x.UtcDateTime.Date))
            .Select(x => new PurchaseDateCount(x.Key, x.Count()))
            .OrderBy(x => x.PurchaseDate)
            .ToList();
    }

    public async Task<PaddockOptionCount> PaddockOptionsAsync(CancellationToken cancellationToken)
    {
        var passes = await db.PaddockPasses.ToListAsync(cancellationToken);
        return new PaddockOptionCount(
            passes.Count,
            passes.Count(x => x.PitLane),
            passes.Count(x => x.Food),
            passes.Count(x => x.Drinks)
        );
    }
}
