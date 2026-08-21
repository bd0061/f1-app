using Microsoft.EntityFrameworkCore;

namespace F1Reporting.Api;

public class ReportedTicket
{
    // Reporting baza čuva samo podatke potrebne za statistiku, ne ceo Ticket.
    public Guid Id { get; set; }
    public DateTimeOffset PurchasedAt { get; set; }
    public List<ReportedTicketDay> Days { get; set; } = [];
}

public class ReportedTicketDay
{
    // Ovaj zapis omogućava brojanje karata po svakom danu trke.
    public int Id { get; set; }
    public Guid TicketId { get; set; }
    public ReportedTicket Ticket { get; set; } = null!;
    public DateOnly RaceDayDate { get; set; }
}

public class ReportedPaddockPass
{
    // Čuvamo samo opcije koje se prikazuju u reporting statistici.
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public bool PitLane { get; set; }
    public bool Food { get; set; }
    public bool Drinks { get; set; }
}

public class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    public DbSet<ReportedTicket> Tickets => Set<ReportedTicket>();
    public DbSet<ReportedTicketDay> TicketDays => Set<ReportedTicketDay>();
    public DbSet<ReportedPaddockPass> PaddockPasses => Set<ReportedPaddockPass>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReportedTicket>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity
                .HasMany(x => x.Days)
                .WithOne(x => x.Ticket)
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ReportedTicketDay>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TicketId, x.RaceDayDate }).IsUnique();
            entity.Property(x => x.RaceDayDate).HasColumnType("date");
        });
        modelBuilder.Entity<ReportedPaddockPass>(entity => entity.HasKey(x => x.Id));
    }
}

public record TicketDayCount(DateOnly RaceDay, int Tickets);

public record PurchaseDateCount(DateOnly PurchaseDate, int Purchases);

public record PaddockOptionCount(int TotalPasses, int PitLane, int Food, int Drinks);
