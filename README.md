# F1 Prodaja tiketa

Ovaj repozitorijum sadrži frontend i backend za dve odvojene aplikacije:

- **A.1 F1 Ticketing API**: kupovina i izmena karata i paddock pass-ova.
- **A.2 Reporting API**: prikupljanje događaja iz RabbitMQ-a i statistika za organizatore.


## 1. Pokretanje 

### Preduslovi

Potrebno instalirati:
- .NET 8 SDK
- Docker Desktop sa uključenim Docker Compose-om



### Pokretanje Docker kontejnera (docker compose)

Iz root direktorijuma projekta pokrenuti:

```powershell
docker compose up -d
```

Compose pokreće:

| Servis | Port | Namena |
|---|---:|---|
| PostgreSQL za A.1 | 5432 | Glavna baza za karte |
| PostgreSQL za A.2 | 5433 | Odvojena reporting baza |
| Redis | 6379 | Keš javnih podataka o trci |
| RabbitMQ | 5672 | Razmena događaja između A.1 i A.2 |
| RabbitMQ Management | 15672 | Web interfejs za pregled brokera |


### Pokretanje A.1

Backend:

Iz `.\backend\F1Ticketing.Api`

```powershell
dotnet run --project F1Ticketing.Api
```

A.1 Swagger:

```text
http://localhost:5284/swagger
```

Pri pokretanju A.1:

1. EF Core se povezuje na PostgreSQL.
2. `Database.Migrate()` primenjuje migraciju `InitialPostgres`.
3. Ako je baza prazna, dodaju se početna trka i valuta EUR.
4. API počinje da prima zahteve.

Frontend:

Iz `.\frontend\ticketing`

```powershell
npm install
npm run dev
```

Dev server na `http://localhost:5173`. Vite dev proxy salje `/api/*` to `http://localhost:5284`(izbegavanje CORS pravila za laksi development)


### Pokretanje A.2

Backend:

Iz `.\backend\F1Reporting.Api`

```powershell
dotnet run --project F1Reporting.Api
```

A.2 Swagger:

```text
http://localhost:5167/swagger
```

Pri pokretanju A.2:

1. EF Core primenjuje migraciju reporting baze.
2. RabbitMQ consumer kreira ili pronalazi exchange i queue.
3. Consumer se vezuje za `f1.ticket-events`.
4. A.2 čeka nove događaje i upisuje reporting podatke.

Frontend:

Frontend:

Iz `.\frontend\reporting`

```powershell
npm install
npm run dev
```

Dev server na `http://localhost:5174`. Vite dev proxy salje `/api/*` do `http://localhost:5167`(izbegavanje CORS pravila za laksi development)

### RabbitMQ Management

Web Interface:

```text
http://localhost:15672
```

Podaci za prijavu:

```text
Korisničko ime: f1
Lozinka: f1
```

### Zaustavljanje infrastrukture

```powershell
docker compose down
```

Za brisanje i PostgreSQL podataka:

```powershell
docker compose down -v
```

## 2. Struktura A.1

A.1 je ASP.NET Core Minimal API projekat.

- `Program.cs`: registracija servisa, migracije, početni podaci i HTTP endpoint-i.
- `Services.cs`: poslovna logika, obračun cena, Redis keš i RabbitMQ publisher.
- `Outbox.cs`: upis događaja u outbox, dispatcher i pozadinski publisher worker.
- `Entities/`: klase domena, na primer `Race`, `Ticket`, `TicketDay`, `Currency` i `PaddockPass`.
- `Persistence/TicketDbContext.cs`: EF Core kontekst i eksplicitno podešavanje relacija.
- `Persistence/Migrations/`: PostgreSQL migracije.
- `Contracts.cs`: ulazni i izlazni modeli API-ja.
- `F1Ticketing.Tests/`: testovi poslovnih pravila.

Glavni A.1 endpoint-i:

```text
GET  /api/race
GET  /api/currencies
PUT  /api/admin/race
POST /api/tickets
PUT  /api/tickets
POST /api/tickets/cancel
POST /api/paddock-passes
```

A.1 čuva podatke u PostgreSQL bazi. Redis i RabbitMQ nisu primarno spremište podataka.

## 3. PostgreSQL model A.1

Najvažnije relacije su:

```text
Race 1 --- više RaceDay zapisa
Race 1 --- više SeatingZone zapisa
Currency 1 --- više Ticket zapisa
Ticket 1 --- više TicketDay zapisa
Ticket 1 --- 0 ili 1 PaddockPass zapis
TicketDay --- 1 RaceDay
TicketDay --- 1 SeatingZone
```

`TicketDay` je spojni zapis. On predstavlja da je određena karta izabrala određeni dan i zonu, zajedno sa cenom sačuvanom u trenutku kupovine.

Valute su globalne. A.1 administracija preko `PUT /api/admin/race` prima listu dozvoljenih valuta. Postojeće valute se uključuju ili isključuju pomoću `IsAllowed`, umesto brisanja, da istorijske karte ne bi ostale bez svoje valute.

## 4. Redis keširanje u A.1

Endpoint `GET /api/race` prvo poziva `IRaceCache`.

- `RedisRaceCache` koristi Redis ključ `f1:race`.
- Ako ključ postoji, odgovor se vraća iz Redis-a.
- Ako ključ ne postoji, podaci se čitaju iz PostgreSQL-a.
- Serijalizovani odgovor se tada upisuje u Redis.
- Keš traje deset minuta.
- Nakon uspešne izmene preko `PUT /api/admin/race`, aplikacija prvo sačuva
	promene u PostgreSQL-u.
- Zatim briše ključ `f1:race` pomoću `InvalidateAsync()`.
- Na kraju upisuje novu verziju podataka u Redis, tako da sledeći `GET`
	dobija sveže podatke.

Redosled izgleda ovako:

```text
GET /api/race
		-> Redis pogodak: vrati keširani JSON
		-> Redis promašaj: pročitaj PostgreSQL, upiši Redis, vrati JSON

PUT /api/admin/race
		-> sačuvaj novu trku u PostgreSQL
		-> obriši stari Redis ključ
		-> upiši novu vrednost u Redis
```

Ako Redis nije dostupan ili nema konekcionog stringa, aplikacija može da radi bez keša i čita direktno iz PostgreSQL-a.

## 5. RabbitMQ i outbox u A.1

A.1 je publisher, ali `TicketService` ne šalje događaj direktno u RabbitMQ, vec koristi outbox obrazac. Događaj se prvo čuva u A.1 PostgreSQL bazi, a poseban background worker ga kasnije objavljuje. Na ovaj nacin izbegavamo situaciju da je jedna od operacija objvaljivanja na queue ili upisa u bazu uspela, a druga ne, cime se stvara nekonzistentnost u sistemu.

Direktno slanje bi imalo dva odvojena koraka:

```text
sačuvaj kartu u PostgreSQL
objavi događaj u RabbitMQ
```

Ako baza uspe, a RabbitMQ privremeno nije dostupan, karta bi postojala bez događaja za A.2. Outbox rešava taj problem tako što kartu i događaj čuva u istoj bazi.

### Šta se upisuje

`OutboxMessage` tabela sadrži:

- `Id`: jedinstveni identifikator poruke.
- `EventName`: na primer `TicketCreated`.
- `Payload`: JSON podaci događaja.
- `OccurredAt`: vreme nastanka događaja.
- `PublishedAt`: vreme uspešnog slanja, ili `null` dok poruka čeka.
- `Attempts`: broj neuspešnih pokušaja.
- `LastError`: poslednja greška ako je slanje propalo.

Kada se kupi karta, `TicketService` radi sledeće:

```text
1. napravi Ticket i TicketDay zapise
2. napravi OutboxMessage zapis
3. pozove SaveChangesAsync()
```

Pošto se sve čuva kroz isti EF Core DbContext i PostgreSQL transakciju, ili se čuvaju i karta i događaj, ili se ne čuva nijedno.

`RabbitEventPublisher`:

1. Čita RabbitMQ konekciju iz konfiguracije.
2. Otvara konekciju i kanal.
3. Kreira trajni fanout exchange `f1.ticket-events`.
4. Serijalizuje naziv događaja, payload i vreme događaja u JSON.
5. Objavljuje poruku na exchange.

`OutboxPublisherWorker` se pokreće kao ASP.NET Core `BackgroundService`. Na svaka dva sekunda otvara scope, čita do 50 neposlatih outbox poruka i prosleđuje ih `OutboxDispatcher`-u. Dispatcher šalje poruke redom po `OccurredAt` vrednosti.

Ako slanje uspe:

```text
objavi poruku
postavi PublishedAt
sačuvaj promenu u bazi
```

Ako slanje ne uspe:

```text
uvećaj Attempts
sačuvaj LastError
ostavi PublishedAt = null
```

Sledeći prolaz worker-a ponovo pokušava slanje. Zapis se ne gubi zato što je RabbitMQ bio nedostupan.

Objavljuju se događaji:

- `TicketCreated`
- `TicketModified`
- `TicketCancelled`
- `PaddockPassCreated`

A.1 sada koristi outbox obrazac. Ovo rešava gubitak događaja zbog privremenog pada RabbitMQ-a. Trenutna jednostavna verzija još nema dead-letter queue, ograničenje broja pokušaja, backoff između pokušaja niti distribuirano zaključavanje za više worker instanci.

## 6. Struktura A.2

A.2 je posebna ASP.NET Core Minimal API aplikacija sa sopstvenom bazom.

- `Program.cs`: konfiguracija reporting baze, migracije i read-only endpoint-i.
- `ReportingModels.cs`: reporting entiteti i `ReportingDbContext`.
- `ReportingConsumer.cs`: dugotrajni background servis koji sluša RabbitMQ queue.
- `ReportingEventProcessor.cs`: parsiranje JSON događaja i upis u reporting bazu.
- `ReportingQueries.cs`: statistički upiti.
- `Persistence/Migrations/`: migracije odvojene reporting baze.
- `F1Reporting.Tests/`: testovi događaja i statistika.

Reporting endpoint-i:

```text
GET /api/reports/tickets-by-race-day
GET /api/reports/purchases-by-date?from=2026-01-01
GET /api/reports/paddock-passes
```

## 7. RabbitMQ consumer u A.2

A.2 kreira trajni queue `f1.reporting` i vezuje ga za fanout exchange `f1.ticket-events`.

Consumer koristi ručne potvrde poruka:

- Poruka se čita.
- Događaj se parsira i upisuje u reporting PostgreSQL bazu.
- Tek nakon uspešnog upisa šalje se `BasicAck`.
- Ako obrada padne, šalje se `BasicNack` bez ponovnog vraćanja poruke u queue.

A.2 obrađuje `TicketCreated` i `PaddockPassCreated`. Događaji izmene i otkazivanja se trenutno namerno ignorišu, jer reporting zahtevi traže samo kupljene karte i pass-ove.

Procesor proverava da li ID već postoji pre upisa. Zbog toga ponovljena ista poruka ne pravi duplikat reporting zapisa.



## 8. Testiranje

Pokretanje svih testova:

```powershell
dotnet test
```

Jedinični testovi koriste EF Core in memory provider radi brzine. E2E testovi u `InfrastructureE2ETests.cs` koriste Testcontainers i stvarne Docker kontejnere za PostgreSQL, RabbitMQ i Redis. Oni proveravaju stvarni A.1 outbox upis, dispatcher, RabbitMQ publish, PostgreSQL reporting upis, Redis TTL i invalidaciju. Puni A.2 `BackgroundService` lifecycle još nije pokrenut kao host u testu, poruka se nakon stvarnog publisha predaje processoru direktno.

### Specifikacija

Relevantni dijagrami i plantuml kod iz kojih su generisani nalaze se u `.\docs\` direktorijumu. Detaljnija dokumentacija o samom projektu se takođe može tu naći.

