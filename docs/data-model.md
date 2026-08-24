# Logical Data Model

## Architecture Rationale

Architecture C uses two PostgreSQL data stores. The ticketing database is the transactional source of truth for race configuration, customers, tickets, prices, and the outbox. The reporting database is a separate read model populated by RabbitMQ events. A.2 therefore does not query A.1 tables directly and reporting work cannot block a ticket purchase.

Redis stores a serialized race-information cache with a time-to-live. It is infrastructure, not a relational entity. RabbitMQ transports published integration events. The outbox bridges the ticketing transaction and RabbitMQ publication so an event is not emitted before the corresponding ticketing data is committed.

## Ticketing PostgreSQL Model

| Entity          | Primary key | Important fields                                                                                                       | Relationships                                                             |
| --------------- | ----------- | ---------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| `Race`          | `Id`        | `Name`, `Location`, `StartDate`, `EndDate`, `AdditionalInformation`, `DiscountUntil`                                   | One race has many `RaceDay` and `SeatingZone` rows.                       |
| `RaceDay`       | `Id`        | `RaceId`, `Date`, `BasePrice`, `Capacity`                                                                              | Many days belong to one race; a day occurs in many `TicketDay` rows.      |
| `SeatingZone`   | `Id`        | `RaceId`, `Name`, `Characteristics`, `Capacity`, `Surcharge`                                                           | Many zones belong to one race; a zone occurs in many `TicketDay` rows.    |
| `Currency`      | `Id`        | `Code`, `IsAllowed`                                                                                                    | One currency can be selected by many `Ticket` rows.                       |
| `Ticket`        | `Id` (GUID) | `RegistrationCode`, `PromoCode`, `PromoCodeUsed`, customer fields, `CurrencyId`, `TotalPrice`, `Status`, `PurchasedAt` | A ticket has many `TicketDay` rows and at most one `PaddockPass`.         |
| `TicketDay`     | `Id`        | `TicketId`, `RaceDayId`, `SeatingZoneId`, `Price`                                                                      | Join/history row for one ticket, one race day, and one seating zone.      |
| `PaddockPass`   | `Id` (GUID) | `TicketId`, `PitLane`, `Food`, `Drinks`, `TotalPrice`                                                                  | Zero or one pass belongs to one ticket.                                   |
| `OutboxMessage` | `Id` (GUID) | event name/type, serialized payload, occurred/published state                                                          | Created in the same transaction as ticketing changes and later published. |

Cardinalities are `Race 1..* RaceDay`, `Race 1..* SeatingZone`, `Currency 1..* Ticket`, `Ticket 1..* TicketDay`, `RaceDay 1..* TicketDay`, `SeatingZone 1..* TicketDay`, and `Ticket 0..1 PaddockPass`.

`TicketDay.Price` is a price snapshot. It preserves the amount charged for that day and zone even if administration later changes the race configuration. A ticket references a currency; the final persisted ticket total is computed by the ticketing service after discounts and, where needed, conversion from EUR.

## Reporting PostgreSQL Model

| Entity                | Primary key        | Important fields                        | Relationships                                                                           |
| --------------------- | ------------------ | --------------------------------------- | --------------------------------------------------------------------------------------- |
| `ReportedTicket`      | `Id` (ticket GUID) | `PurchasedAt`                           | A minimal projection of an original ticket purchase; has many `ReportedTicketDay` rows. |
| `ReportedTicketDay`   | `Id`               | `TicketId`, `RaceDayDate`               | Many rows belong to one reported ticket; unique on `(TicketId, RaceDayDate)`.           |
| `ReportedPaddockPass` | `Id` (pass GUID)   | `TicketId`, `PitLane`, `Food`, `Drinks` | Minimal projection of one paddock event.                                                |

The report database deliberately stores only fields needed for the three reports. `ReportedTicket` makes the purchase-date report count each ticket exactly once. `ReportedTicketDay` enables counts grouped by selected competition day. `ReportedPaddockPass` provides option totals. The current consumer does not apply ticket modification/cancellation events to these projections; this preserves the specified original-purchase count behavior.

## Data Ownership

- A.1 React sends commands and reads public configuration through the ticketing API only.
- Ticketing PostgreSQL owns mutable business records and writes `OutboxMessage` records.
- Redis caches the public `GET /api/race` response and is invalidated after a race update.
- RabbitMQ publishes committed ticket/paddock events from the outbox worker.
- The reporting consumer owns the write path into reporting PostgreSQL.
- A.2 React reads only reporting API endpoints and never accesses the ticketing database.
