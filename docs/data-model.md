# Logički model podataka

## Obrazloženje arhitekture

Arhitektura C koristi dva PostgreSQL skladišta podataka. Baza podataka za prodaju karata predstavlja transakcioni izvor istine za konfiguraciju trke, korisnike, karte, cene i outbox. Baza podataka za izveštavanje predstavlja zaseban model za čitanje koji se popunjava putem RabbitMQ događaja. A.2 stoga ne pristupa direktno tabelama A.1, a rad na izveštavanju ne može blokirati kupovinu karte.

Redis čuva serijalizovani keš informacija o trci sa vremenom isteka. On predstavlja infrastrukturnu komponentu, a ne relacioni entitet. RabbitMQ prenosi objavljene integracione događaje. Outbox povezuje transakciju prodaje karata sa objavljivanjem na RabbitMQ, tako da se događaj ne emituje pre nego što odgovarajući podaci o karti budu potvrđeni.

## Ticketing PostgreSQL model

| Entitet | Primarni ključ | Važna polja | Relacije |
| --------------- | ----------- | ---------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| `Race` | `Id` | `Name`, `Location`, `StartDate`, `EndDate`, `AdditionalInformation`, `DiscountUntil` | Jedna trka ima više redova `RaceDay` i `SeatingZone`. |
| `RaceDay` | `Id` | `RaceId`, `Date`, `BasePrice`, `Capacity` | Više dana pripada jednoj trci; jedan dan se pojavljuje u više redova `TicketDay`. |
| `SeatingZone` | `Id` | `RaceId`, `Name`, `Characteristics`, `Capacity`, `Surcharge` | Više zona pripada jednoj trci; jedna zona se pojavljuje u više redova `TicketDay`. |
| `Currency` | `Id` | `Code`, `IsAllowed` | Jednu valutu može izabrati više redova `Ticket`. |
| `Ticket` | `Id` (GUID) | `RegistrationCode`, `PromoCode`, `PromoCodeUsed`, polja korisnika, `CurrencyId`, `TotalPrice`, `Status`, `PurchasedAt` | Jedna karta ima više redova `TicketDay` i najviše jednu `PaddockPass` propusnicu. |
| `TicketDay` | `Id` | `TicketId`, `RaceDayId`, `SeatingZoneId`, `Price` | Povezni/istorijski red za jednu kartu, jedan dan trke i jednu zonu za sedenje. |
| `PaddockPass` | `Id` (GUID) | `TicketId`, `PitLane`, `Food`, `Drinks`, `TotalPrice` | Nula ili jedna propusnica pripada jednoj karti. |
| `OutboxMessage` | `Id` (GUID) | naziv/tip događaja, serijalizovani sadržaj, stanje nastanka/objavljivanja | Kreira se u istoj transakciji kao i promene vezane za prodaju karata, a kasnije se objavljuje. |

Kardinalnosti su `Race 1..\* RaceDay`, `Race 1..\* SeatingZone`, `Currency 1..\* Ticket`, `Ticket 1..\* TicketDay`, `RaceDay 1..\* TicketDay`, `SeatingZone 1..\* TicketDay` i `Ticket 0..1 PaddockPass`.

`TicketDay.Price` predstavlja snimak cene. On čuva iznos naplaćen za taj dan i zonu čak i ako administracija kasnije promeni konfiguraciju trke. Karta referencira valutu; konačan sačuvani ukupan iznos karte izračunava servis za prodaju karata nakon popusta i, gde je potrebno, konverzije iz EUR.

## Reporting PostgreSQL model

| Entitet | Primarni ključ | Važna polja | Relacije |
| --------------------- | ------------------ | --------------------------------------- | --------------------------------------------------------------------------------------- |
| `ReportedTicket` | `Id` (GUID karte) | `PurchasedAt` | Minimalna projekcija originalne kupovine karte; ima više redova `ReportedTicketDay`. |
| `ReportedTicketDay` | `Id` | `TicketId`, `RaceDayDate` | Više redova pripada jednoj prijavljenoj karti; jedinstveno po `(TicketId, RaceDayDate)`. |
| `ReportedPaddockPass` | `Id` (GUID propusnice) | `TicketId`, `PitLane`, `Food`, `Drinks` | Minimalna projekcija jednog paddock događaja. |

Baza podataka za izveštavanje namerno čuva samo polja potrebna za tri izveštaja. `ReportedTicket` omogućava da izveštaj o kupovinama po datumu broji svaku kartu tačno jednom. `ReportedTicketDay` omogućava brojanje grupisano po izabranom danu takmičenja. `ReportedPaddockPass` omogućava izračunavanje ukupnog broja opcija. Trenutni consumer ne primenjuje događaje izmene/otkazivanja karata na ove projekcije; time se zadržava definisano ponašanje brojanja originalnih kupovina.

## Vlasništvo nad podacima

- A.1 React šalje komande i čita javnu konfiguraciju isključivo putem ticketing API-ja.
- Ticketing PostgreSQL poseduje promenljive poslovne podatke i upisuje `OutboxMessage` zapise.
- Redis kešira odgovor javnog `GET /api/race` endpointa i invalidira se nakon ažuriranja trke.
- RabbitMQ objavljuje potvrđene događaje vezane za karte/paddock iz outbox worker-a.
- Reporting consumer poseduje putanju upisa u reporting PostgreSQL.
- A.2 React čita isključivo reporting API endpoint-e i nikada ne pristupa ticketing bazi podataka.