using F1Reporting.Api;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// A.2 ima sopstvenu bazu. Ne čita direktno A.1 bazu, već podatke dobija kroz
// događaje iz RabbitMQ-a.
builder.Services.AddDbContext<ReportingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);
builder.Services.AddHostedService<ReportingConsumer>();
builder.Services.AddScoped<ReportingQueries>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
    // Reporting baza sama primenjuje svoje migracije pri pokretanju.
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

// Ovi endpoint-i samo čitaju lokalnu reporting bazu; RabbitMQ je puni u
// pozadini, pa se rezultati mogu osvežavati bez poziva ka A.1.
app.MapGet(
    "/api/reports/tickets-by-race-day",
    (ReportingQueries queries, CancellationToken ct) => queries.TicketsByRaceDayAsync(ct)
);
app.MapGet(
    "/api/reports/purchases-by-date",
    (DateOnly? from, ReportingQueries queries, CancellationToken ct) =>
        queries.PurchasesByDateAsync(from, ct)
);
app.MapGet(
    "/api/reports/paddock-passes",
    (ReportingQueries queries, CancellationToken ct) => queries.PaddockOptionsAsync(ct)
);

app.Run();

public partial class Program { }
