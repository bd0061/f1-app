using Microsoft.EntityFrameworkCore;

namespace F1Ticketing.Api;

public class TicketDbContext(DbContextOptions<TicketDbContext> options) : DbContext(options)
{
    public DbSet<Race> Races => Set<Race>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<RaceDay> RaceDays => Set<RaceDay>();
    public DbSet<SeatingZone> SeatingZones => Set<SeatingZone>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketDay> TicketDays => Set<TicketDay>();
    public DbSet<PaddockPass> PaddockPasses => Set<PaddockPass>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ovde eksplicitno opisujemo veze i ograničenja relacione baze.
        // EF Core će na osnovu ove konfiguracije napraviti strane ključeve,
        // indekse i pravila brisanja u PostgreSQL šemi.
        modelBuilder.Entity<Race>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity
                .HasMany(x => x.RaceDays)
                .WithOne(x => x.Race)
                .HasForeignKey(x => x.RaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasMany(x => x.SeatingZones)
                .WithOne(x => x.Race)
                .HasForeignKey(x => x.RaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.StartDate).HasColumnType("date");
            entity.Property(x => x.EndDate).HasColumnType("date");
            entity.Property(x => x.DiscountUntil).HasColumnType("date");
        });

        modelBuilder.Entity<Currency>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(3).IsRequired();
            entity
                .HasMany(x => x.Tickets)
                .WithOne(x => x.Currency)
                .HasForeignKey(x => x.CurrencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RaceDay>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.RaceId, x.Date }).IsUnique();
            entity.Property(x => x.Date).HasColumnType("date");
            entity.Property(x => x.BasePrice).HasPrecision(12, 2);
        });
        modelBuilder.Entity<SeatingZone>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.RaceId, x.Name }).IsUnique();
            entity.Property(x => x.Surcharge).HasPrecision(12, 2);
        });
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.RegistrationCode).IsUnique();
            entity.HasIndex(x => x.PromoCode).IsUnique();
            entity.Property(x => x.TotalPrice).HasPrecision(12, 2);
            entity.Property(x => x.CurrencyId).IsRequired();
            entity
                .HasMany(x => x.Days)
                .WithOne(x => x.Ticket)
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(x => x.PaddockPass)
                .WithOne(x => x.Ticket)
                .HasForeignKey<PaddockPass>(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<TicketDay>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity
                .HasOne(x => x.RaceDay)
                .WithMany()
                .HasForeignKey(x => x.RaceDayId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(x => x.SeatingZone)
                .WithMany()
                .HasForeignKey(x => x.SeatingZoneId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TicketId, x.RaceDayId }).IsUnique();
            entity.Property(x => x.Price).HasPrecision(12, 2);
        });
        modelBuilder.Entity<PaddockPass>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TotalPrice).HasPrecision(12, 2);
        });
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            // Outbox je deo A.1 baze, pa Ticket i njegov događaj mogu biti
            // sačuvani atomarno kroz isti DbContext i PostgreSQL transakciju.
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PublishedAt, x.OccurredAt });
            entity.Property(x => x.EventName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Payload).IsRequired();
        });
    }
}
