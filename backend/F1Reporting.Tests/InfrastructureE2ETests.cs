using F1Reporting.Api;
using F1Ticketing.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace F1Reporting.Tests;

// Ova kolekcija omogućava da svi testovi koriste isti skup kontejnera.
// Kontejneri se zato podižu jednom po test klasi, a ne jednom po test metodi.
[CollectionDefinition("Infrastructure")]
public sealed class InfrastructureCollection : ICollectionFixture<InfrastructureFixture> { }

public sealed class InfrastructureFixture : IAsyncLifetime
{
    // A.1 ima sopstvenu PostgreSQL bazu. Test koristi pravi PostgreSQL server,
    // a ne EF InMemory provider, kako bi se proverile stvarne SQL relacije.
    public PostgreSqlContainer TicketingPostgres { get; } =
        new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("f1")
            .WithUsername("f1")
            .WithPassword("f1")
            .Build();

    // A.2 namerno koristi drugu PostgreSQL instancu. Reporting sistem je
    // odvojen od sistema za prodaju i podatke dobija kroz događaje.
    public PostgreSqlContainer ReportingPostgres { get; } =
        new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("f1_reporting")
            .WithUsername("f1")
            .WithPassword("f1")
            .Build();

    // Ovo je pravi RabbitMQ broker kroz koji prolazi TicketCreated poruka.
    public RabbitMqContainer RabbitMq { get; } =
        new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management-alpine")
            .WithUsername("f1")
            .WithPassword("f1")
            .Build();

    // Redis testira stvarni ključ, vrednost, TTL i brisanje keša.
    public RedisContainer Redis { get; } = new RedisBuilder().WithImage("redis:7-alpine").Build();

    // Testcontainers pokreće sva četiri servisa paralelno i čeka da svaki
    // prijavi da je spreman za konekcije.
    public Task InitializeAsync() =>
        Task.WhenAll(
            TicketingPostgres.StartAsync(),
            ReportingPostgres.StartAsync(),
            RabbitMq.StartAsync(),
            Redis.StartAsync()
        );

    // Nakon testova kontejnere gasimo i uklanjamo. Podaci su izolovani za
    // sledeće pokretanje, pa jedan test ne utiče na drugi.
    public Task DisposeAsync() =>
        Task.WhenAll(
            TicketingPostgres.DisposeAsync().AsTask(),
            ReportingPostgres.DisposeAsync().AsTask(),
            RabbitMq.DisposeAsync().AsTask(),
            Redis.DisposeAsync().AsTask()
        );
}

[Collection("Infrastructure")]
public sealed class InfrastructureE2ETests(InfrastructureFixture infrastructure)
{
    [Fact]
    public async Task Ticket_event_travels_through_rabbitmq_into_reporting_postgres()
    {
        // Kreiramo DbContext-e koji koriste connection stringove dinamički
        // dodeljenih Testcontainers PostgreSQL instanci.
        await using var ticketingDb = CreateTicketingDb();
        await using var reportingDb = CreateReportingDb();

        // Migracije moraju biti iste one koje aplikacije koriste na startup-u.
        // Time proveravamo i da se šema stvarno može napraviti u PostgreSQL-u.
        await SeedTicketingDbAsync(ticketingDb);
        await reportingDb.Database.MigrateAsync();

        // Queue i binding se kreiraju pre kupovine. Fanout exchange ne čuva
        // staru poruku za queue koji nije postojao u trenutku objave.
        await using var capture = CreateRabbitCapture();
        var publisher = new RabbitEventPublisher(
            Configuration(
                "RabbitMq:ConnectionString",
                infrastructure.RabbitMq.GetConnectionString()
            ),
            NullLogger<RabbitEventPublisher>.Instance
        );
        // Zahtev prolazi kroz isti TicketService koji koristi A.1 API.
        var request = new PurchaseTicketRequest(
            new CustomerRequest(
                "Ana",
                "Petrovic",
                "Main Street 1",
                "10000",
                "Belgrade",
                "Serbia",
                "ana@example.com",
                "ana@example.com"
            ),
            [new TicketDayRequest(new DateOnly(2027, 5, 21), "Grandstand")],
            "EUR",
            null
        );
        var service = new TicketService(
            ticketingDb,
            new FixedRate(),
            new OutboxWriter(ticketingDb),
            NullLogger<TicketService>.Instance
        );

        // Ovo upisuje kartu i TicketCreated zapis u A.1 outbox tabelu.
        var ticket = await service.PurchaseAsync(request, CancellationToken.None);

        // Dispatcher čita pending outbox zapis, objavljuje ga u stvarni
        // RabbitMQ exchange i označava ga kao uspešno poslatog.
        var dispatcher = new OutboxDispatcher(
            ticketingDb,
            publisher,
            NullLogger<OutboxDispatcher>.Instance
        );
        Assert.Equal(1, await dispatcher.PublishPendingAsync(CancellationToken.None));

        // Test čeka stvarne bajtove koji su prošli kroz RabbitMQ. Timeout
        // štiti test od beskonačnog čekanja ako publisher ili broker ne rade.
        var body = await capture.Message.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Primljena poruka se predaje A.2 procesoru, koji JSON pretvara u
        // reporting zapise u odvojenoj PostgreSQL bazi.
        await new ReportingEventProcessor().ProcessAsync(reportingDb, body);

        // ID mora ostati isti kroz ceo tok, a svaki izabrani dan mora dobiti
        // svoj reporting zapis.
        Assert.Equal(ticket.Id, (await reportingDb.Tickets.SingleAsync()).Id);
        Assert.Single(await reportingDb.TicketDays.ToListAsync());
    }

    [Fact]
    public async Task Redis_stores_race_json_with_ttl_and_invalidation_removes_it()
    {
        // Koristimo produkcijsku RedisRaceCache klasu, samo joj prosleđujemo
        // connection string stvarnog Testcontainers Redis servera.
        var cache = new RedisRaceCache(
            Configuration("ConnectionStrings:Redis", infrastructure.Redis.GetConnectionString()),
            NullLogger<RedisRaceCache>.Instance
        );
        // SetAsync upisuje JSON pod ključem f1:race sa TTL-om od 10 minuta.
        await cache.SetAsync("{\"name\":\"Monaco Grand Prix\"}");

        await using var database = await ConnectionMultiplexer.ConnectAsync(
            infrastructure.Redis.GetConnectionString()
        );
        // Direktnim Redis upitima proveravamo i sadržaj i stvarni preostali TTL.
        var value = await database.GetDatabase().StringGetAsync("f1:race");
        var ttl = await database.GetDatabase().KeyTimeToLiveAsync("f1:race");
        // Invalidacija simulira uspešnu admin izmenu trke.
        await cache.InvalidateAsync();

        Assert.Equal("{\"name\":\"Monaco Grand Prix\"}", value.ToString());
        Assert.True(
            ttl is not null && ttl.Value > TimeSpan.Zero && ttl.Value <= TimeSpan.FromMinutes(10)
        );
        Assert.False(await database.GetDatabase().KeyExistsAsync("f1:race"));
    }

    // Kreira privremeni, ekskluzivni queue i veže ga za A.1 fanout exchange.
    // Queue hvata poruku samo za ovaj test i uklanja se kada se konekcija zatvori.
    private RabbitCapture CreateRabbitCapture()
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(infrastructure.RabbitMq.GetConnectionString()),
        };
        var connection = factory.CreateConnection();
        var channel = connection.CreateModel();
        channel.ExchangeDeclare("f1.ticket-events", ExchangeType.Fanout, durable: true);
        var queue = channel.QueueDeclare().QueueName;
        channel.QueueBind(queue, "f1.ticket-events", "");
        var capture = new RabbitCapture(connection, channel);
        var consumer = new EventingBasicConsumer(channel);
        // Manual ack potvrđuje RabbitMQ-u da je test poruku uspešno primio.
        consumer.Received += (_, args) =>
        {
            capture.Message.TrySetResult(args.Body.ToArray());
            channel.BasicAck(args.DeliveryTag, false);
        };
        channel.BasicConsume(queue, autoAck: false, consumer);
        return capture;
    }

    // DbContext za A.1 koristi port koji je Testcontainers izložio na hostu.
    private TicketDbContext CreateTicketingDb() =>
        new(
            new DbContextOptionsBuilder<TicketDbContext>()
                .UseNpgsql(infrastructure.TicketingPostgres.GetConnectionString())
                .Options
        );

    // DbContext za A.2 se povezuje na potpuno odvojenu bazu.
    private ReportingDbContext CreateReportingDb() =>
        new(
            new DbContextOptionsBuilder<ReportingDbContext>()
                .UseNpgsql(infrastructure.ReportingPostgres.GetConnectionString())
                .Options
        );

    // Seedujemo samo minimalne podatke potrebne za kupovinu karte. Produkcijski
    // sample race nije deo ovog testa jer test treba da bude mali i determinističan.
    private static async Task SeedTicketingDbAsync(TicketDbContext db)
    {
        await db.Database.MigrateAsync();
        db.Races.Add(
            new Race
            {
                DiscountUntil = new DateOnly(2027, 1, 1),
                RaceDays =
                [
                    new RaceDay
                    {
                        Date = new DateOnly(2027, 5, 21),
                        BasePrice = 100,
                        Capacity = 100,
                    },
                ],
                SeatingZones =
                [
                    new SeatingZone
                    {
                        Name = "Grandstand",
                        Capacity = 100,
                        Surcharge = 25,
                    },
                ],
            }
        );
        db.Currencies.Add(new Currency { Code = "EUR" });
        await db.SaveChangesAsync();
    }

    // Pravimo konfiguraciju u memoriji, bez menjanja appsettings fajlova.
    private static IConfiguration Configuration(string key, string value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();

    // Test ne treba da poziva javni kursni API, zato vraćamo kurs 1:1.
    private sealed class FixedRate : IExchangeRateService
    {
        public Task<decimal> GetRateAsync(
            string from,
            string to,
            CancellationToken cancellationToken
        ) => Task.FromResult(1m);
    }

    // Pomoćni objekat drži RabbitMQ resurse i završava Task kada stigne poruka.
    private sealed class RabbitCapture(IConnection connection, IModel channel) : IAsyncDisposable
    {
        public TaskCompletionSource<byte[]> Message { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Zatvaranjem channel-a i connection-a privremeni queue se automatski
        // čisti, a test ne ostavlja RabbitMQ resurse iza sebe.
        public ValueTask DisposeAsync()
        {
            channel.Dispose();
            connection.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
