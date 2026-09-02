using Microsoft.EntityFrameworkCore;
using F1Ticketing.Api.Exceptions;
namespace F1Ticketing.Api.Services;

public sealed class TicketService(
    TicketDbContext db,
    IExchangeRateService exchangeRates,
    IOutboxWriter outbox,
    ILogger<TicketService> logger
)
{
    // Kupovina pravi jedan Ticket zapis i po jedan TicketDay zapis za svaki
    // izabrani dan trke.
    public async Task<Ticket> PurchaseAsync(
        PurchaseTicketRequest request,
        CancellationToken cancellationToken
    )
    {
        // 1. Proveravamo obavezna polja kupca i potvrdu email adrese.
        // Korisnički nalozi nisu deo zahteva; email i generisani kod služe
        // kao način pristupa kasnijim izmenama i otkazivanju karte.
        ValidateCustomer(request.Customer);

        // 2. Učitavamo konfiguraciju jedine aktivne trke zajedno sa njenim
        // danima i zonama, jer su svi oni potrebni za izbor i cenu karte.
        var race = await db
            .Races.Include(x => x.RaceDays)
            .Include(x => x.SeatingZones)
            .SingleAsync(cancellationToken);
        // 3. Kupac može da koristi samo valutu koju je administrator ostavio
        // aktivnom u globalnom katalogu valuta.
        var currency =
            await db.Currencies.SingleOrDefaultAsync(
                x => x.Code == request.Currency.ToUpperInvariant() && x.IsAllowed,
                cancellationToken
            ) ?? throw new RuleException("Currency is not allowed.");
        // 4. Svaki zahtevani datum i zona moraju postojati u konfiguraciji.
        var selected = ResolveDays(request.Days, race);
        if (selected.Count == 0)
            throw new RuleException("Select at least one race day.");
        // Ista takmičarska/race day stavka ne sme se kupiti dvaput u jednoj
        // karti, čak i ako je klijent pošalje dva puta u zahtev.
        if (selected.Select(x => x.day.Id).Distinct().Count() != selected.Count)
            throw new RuleException("A race day can only be selected once.");
        foreach (var group in selected.GroupBy(x => new { Day = x.day.Id, Zone = x.zone.Id }))
        {
            // Kapacitet se proverava u kombinaciji dan + zona. Broj već
            // prodatih TicketDay zapisa ne sme preći kapacitet zone.
            var sold = await db.TicketDays.CountAsync(
                x => x.RaceDayId == group.Key.Day && x.SeatingZoneId == group.Key.Zone,
                cancellationToken
            );
            if (sold + group.Count() > group.First().zone.Capacity)
                throw new RuleException("There are not enough seats in the selected zone.");
        }
        // Promo kod pripada prethodnoj aktivnoj karti. Može se iskoristiti
        // samo jednom, a otkazana karta više ne može biti izvor validnog koda.
        var promoOwner = request.PromoCode is null
            ? null
            : await db.Tickets.SingleOrDefaultAsync(
                x => x.PromoCode == request.PromoCode && x.Status == TicketStatus.Active,
                cancellationToken
            );
        if (request.PromoCode is not null && promoOwner is null)
            throw new RuleException("Promo code is invalid.");
        if (promoOwner is not null && promoOwner.PromoCodeUsed)
            throw new RuleException("Promo code has already been used.");
        // 5. Čuvamo lične podatke i vezujemo kartu za Currency entitet.
        // Registracioni kod služi za pristup, a promo kod za buduću kupovinu.
        var ticket = new Ticket
        {
            RegistrationCode = CreateCode(),
            PromoCode = CreateCode(),
            CurrencyId = currency.Id,
            Currency = currency,
            FirstName = request.Customer.FirstName.Trim(),
            LastName = request.Customer.LastName.Trim(),
            Address1 = request.Customer.Address1.Trim(),
            PostalCode = request.Customer.PostalCode.Trim(),
            City = request.Customer.City.Trim(),
            Country = request.Customer.Country.Trim(),
            Email = request.Customer.Email.Trim(),
        };
        // Cena svakog dana je osnovna cena dana plus doplata iz zone. Cena se
        // čuva na TicketDay-u kao istorijski snapshot.
        foreach (var item in selected)
            ticket.Days.Add(
                new TicketDay
                {
                    RaceDayId = item.day.Id,
                    SeatingZoneId = item.zone.Id,
                    Price = item.day.BasePrice + item.zone.Surcharge,
                }
            );
        // 6. Najpre se primenjuje 10% vremenski popust, zatim opcioni promo
        // popust od 5%, pa se EUR iznos konvertuje u izabranu valutu.
        ticket.TotalPrice = await CalculateTotalAsync(
            ticket.Days.Sum(x => x.Price),
            currency.Code,
            race.DiscountUntil,
            promoOwner is not null,
            cancellationToken
        );
        if (promoOwner is not null)
            promoOwner.PromoCodeUsed = true;
        // 7. Bazu čuvamo pre RabbitMQ objave. Događaj se objavljuje tek kada
        // je Ticket sigurno upisan u A.1 PostgreSQL bazu.
        db.Tickets.Add(ticket);
        outbox.Add(
            "TicketCreated",
            new
            {
                ticket.Id,
                ticket.PurchasedAt,
                RaceDays = ticket.Days.Select(day => new
                {
                    selected.Single(x => x.day.Id == day.RaceDayId).day.Date,
                }),
                ticket.Days.Count,
            }
        );
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Saved ticket {TicketId} with {DayCount} race days",
            ticket.Id,
            ticket.Days.Count
        );
        // IOutboxWriter samo priprema TicketCreated zapis u EF change tracker-u.
        // On će biti upisan zajedno sa kartom, pre nego što worker pošalje
        // događaj u RabbitMQ. A.2 koristi ove podatke za svoje izveštaje.
        return ticket;
    }

    public async Task<Ticket> ModifyAsync(
        ModifyTicketRequest request,
        CancellationToken cancellationToken
    )
    {
        // Izmena zahteva registracioni kod i originalni email.
        var ticket =
            await db
                .Tickets.Include(x => x.Days)
                .SingleOrDefaultAsync(
                    x =>
                        x.RegistrationCode == request.RegistrationCode
                        && x.Email == request.Email
                        && x.Status == TicketStatus.Active,
                    cancellationToken
                )
            ?? throw new RuleException("Active ticket was not found.");
        var race = await db
            .Races.Include(x => x.RaceDays)
            .Include(x => x.SeatingZones)
            .SingleAsync(cancellationToken);
        // Prvo uklanjamo tražene dane. Ako bi karta ostala bez ijednog dana,
        // zahtev se odbija ispod, jer karta mora imati bar jedan dan.
        foreach (var remove in request.RemoveDays)
        {
            var existing =
                ticket.Days.SingleOrDefault(x =>
                    race.RaceDays.Single(d => d.Id == x.RaceDayId).Date == remove
                ) ?? throw new RuleException("Ticket does not contain that day.");
            ticket.Days.Remove(existing);
            db.TicketDays.Remove(existing);
        }
        // Zatim proveravamo i pripremamo nove dane i njihove zone.
        var additions = ResolveDays(request.AddDays, race);
        if (ticket.Days.Count + additions.Count == 0)
            throw new RuleException("A ticket must contain at least one day.");
        if (
            ticket
                .Days.Select(x => x.RaceDayId)
                .Concat(additions.Select(x => x.day.Id))
                .Distinct()
                .Count()
            != ticket.Days.Count + additions.Count
        )
            throw new RuleException("A race day can only be selected once.");
        // Kapacitet se ponovo proverava za dodate dane. Trenutna karta se
        // izuzima iz brojača zato što njeni postojeći dani nisu nova prodaja.
        foreach (var group in additions.GroupBy(x => new { Day = x.day.Id, Zone = x.zone.Id }))
        {
            var sold = await db.TicketDays.CountAsync(
                x =>
                    x.RaceDayId == group.Key.Day
                    && x.SeatingZoneId == group.Key.Zone
                    && x.TicketId != ticket.Id,
                cancellationToken
            );
            if (sold + group.Count() > group.First().zone.Capacity)
                throw new RuleException("There are not enough seats in the selected zone.");
        }
        // Dodatni dani dobijaju novi TicketDay zapis sa tada važećom cenom.
        foreach (var item in additions)
            ticket.Days.Add(
                new TicketDay
                {
                    RaceDayId = item.day.Id,
                    SeatingZoneId = item.zone.Id,
                    Price = item.day.BasePrice + item.zone.Surcharge,
                }
            );
        // Kod izmene je dozvoljena i promena valute, ali samo na trenutno
        // aktivnu globalnu valutu. Zatim se cena računa iz svih preostalih
        // i novih dana.
        var currency =
            await db.Currencies.SingleOrDefaultAsync(
                x => x.Code == request.Currency.ToUpperInvariant() && x.IsAllowed,
                cancellationToken
            ) ?? throw new RuleException("Currency is not allowed.");
        ticket.CurrencyId = currency.Id;
        ticket.Currency = currency;
        ticket.TotalPrice = await CalculateTotalAsync(
            ticket.Days.Sum(x => x.Price),
            currency.Code,
            race.DiscountUntil,
            false,
            cancellationToken
        );
        // Izmena karte takođe dobija outbox događaj, iako ga trenutni A.2
        // procesor namerno ignoriše prema zahtevima reporting portala.
        outbox.Add("TicketModified", new { ticket.Id });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Modified ticket {TicketId}; now contains {DayCount} race days",
            ticket.Id,
            ticket.Days.Count
        );
        // Reporting trenutno ignoriše ovaj događaj, ali ga A.1 objavljuje
        // zato što je deo opšteg ugovora o događajima.
        return ticket;
    }

    public async Task CancelAsync(string code, string email, CancellationToken cancellationToken)
    {
        // Otkazivanje koristi isti pristup bez naloga: kod + email moraju
        // pripadati istoj karti.
        var ticket =
            await db.Tickets.SingleOrDefaultAsync(
                x => x.RegistrationCode == code && x.Email == email,
                cancellationToken
            ) ?? throw new RuleException("Ticket was not found.");
        // Otkazivanje je konačno. Ne brišemo zapis, već čuvamo istoriju i
        // menjamo status tako da se karta ne može ponovo aktivirati.
        if (ticket.Status == TicketStatus.Cancelled)
            throw new RuleException("Ticket is already cancelled.");
        ticket.Status = TicketStatus.Cancelled;
        // Promo kod otkazane karte se namerno invalidira promenom vrednosti.
        ticket.PromoCode = "CANCELLED" + ticket.Id.ToString("N");
        // Otkazivanje se čuva kao događaj radi drugih mogućih potrošača, na
        // primer audit servisa, čak i kada ga A.2 trenutno ne koristi.
        outbox.Add("TicketCancelled", new { ticket.Id });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Cancelled ticket {TicketId}", ticket.Id);
    }

    public async Task<PaddockPass> PurchasePaddockAsync(
        PaddockRequest request,
        CancellationToken cancellationToken
    )
    {
        // Paddock pass je dodatak postojećoj aktivnoj karti. Valuta se ne
        // bira ponovo: automatski se preuzima sa Ticket.Currency relacije.
        var ticket =
            await db
                .Tickets.Include(x => x.Currency)
                .SingleOrDefaultAsync(
                    x =>
                        x.RegistrationCode == request.RegistrationCode
                        && x.Status == TicketStatus.Active,
                    cancellationToken
                )
            ?? throw new RuleException("An active race ticket is required.");
        // Jedna karta može imati najviše jedan pass.
        if (await db.PaddockPasses.AnyAsync(x => x.TicketId == ticket.Id, cancellationToken))
            throw new RuleException("A paddock pass already exists for this ticket.");
        var race = await db.Races.SingleAsync(cancellationToken);
        // Garage Access je obavezni osnovni deo i košta 100 EUR. Ostale
        // opcije se doplaćuju samo ako ih kupac izabere.
        var total =
            100m
            + (request.PitLane ? 50m : 0)
            + (request.Food ? 25m : 0)
            + (request.Drinks ? 25m : 0);
        // Na pass-u čuvamo izabrane opcije, dok se valuta izvodi iz karte.
        var pass = new PaddockPass
        {
            TicketId = ticket.Id,
            PitLane = request.PitLane,
            Food = request.Food,
            Drinks = request.Drinks,
            TotalPrice = await CalculateTotalAsync(
                total,
                ticket.Currency.Code,
                race.DiscountUntil,
                false,
                cancellationToken
            ),
        };
        db.PaddockPasses.Add(pass);
        // Paddock događaj sadrži opcije zato što ih A.2 broji u izveštajima.
        outbox.Add(
            "PaddockPassCreated",
            new
            {
                pass.Id,
                pass.TicketId,
                pass.PitLane,
                pass.Food,
                pass.Drinks,
            }
        );
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Saved paddock pass {PassId} for ticket {TicketId}",
            pass.Id,
            ticket.Id
        );
        // A.2 iz ovog događaja broji ukupne pass-ove i svaku opciju posebno.
        return pass;
    }

    private async Task<decimal> CalculateTotalAsync(
        decimal amount,
        string currency,
        DateOnly discountUntil,
        bool promo,
        CancellationToken ct
    )
    {
        // Sve bazne cene su u EUR. Redosled je važan: vremenski popust 10%,
        // zatim promo popust 5% ako postoji, pa konverzija po trenutnom kursu.
        var price = DateOnly.FromDateTime(DateTime.UtcNow) <= discountUntil ? amount * .9m : amount;
        if (promo)
            price *= .95m;
        return Math.Round(price * await exchangeRates.GetRateAsync("EUR", currency, ct), 2);
    }

    private static void ValidateCustomer(CustomerRequest c)
    {
        // Sva polja označena zvezdicom u zahtevu su obavezna. Ovde ne radimo
        // punu email validaciju; proveravamo prisutnost i potvrdu iste adrese.
        if (
            new[]
            {
                c.FirstName,
                c.LastName,
                c.Address1,
                c.PostalCode,
                c.City,
                c.Country,
                c.Email,
            }.Any(string.IsNullOrWhiteSpace)
        )
            throw new RuleException("All customer fields are required.");
        if (!c.Email.Equals(c.EmailConfirmation, StringComparison.OrdinalIgnoreCase))
            throw new RuleException("Email confirmation does not match.");
    }

    private static List<(RaceDay day, SeatingZone zone)> ResolveDays(
        IEnumerable<TicketDayRequest> requests,
        Race race
    )
    {
        // Ulaz koristi datum i naziv zone, dok domen koristi ID vrednosti.
        // Ova metoda prevodi zahtev u konkretne EF entitete ili vraća jasnu
        // grešku ako dan ili zona ne postoje.
        var result = new List<(RaceDay, SeatingZone)>();
        foreach (var request in requests)
        {
            // Datum mora biti jedan od administratorom definisanih dana trke.
            var day =
                race.RaceDays.SingleOrDefault(x => x.Date == request.Date)
                ?? throw new RuleException("Race day was not found.");
            // Naziv zone se poredi bez obzira na velika/mala slova.
            var zone =
                race.SeatingZones.SingleOrDefault(x =>
                    x.Name.Equals(request.Zone, StringComparison.OrdinalIgnoreCase)
                ) ?? throw new RuleException("Seating zone was not found.");
            result.Add((day, zone));
        }
        return result;
    }

    private static string CreateCode() => Convert.ToHexString(Guid.NewGuid().ToByteArray());
}
