# F1 Ticketing and Reporting Mini-Specification

## Scope

The project contains two independent React/Vite single-page applications. A.1 is the public ticketing application at `frontend/testing`; A.2 is the organizer reporting portal at `frontend/reporting`. Both applications use a Vite development proxy and keep browser requests on `/api`. The ticketing backend owns pricing, currency conversion, Redis caching, PostgreSQL persistence, the outbox, and RabbitMQ publication. The reporting backend owns its separate reporting database and consumes RabbitMQ events.

The frontends intentionally use page-local React state, `useEffect`, direct `fetch`, and plain CSS. No account system, client-side exchange-rate integration, shared frontend package, chart package, Redux, or form library is used.

## Feature Map

| Area | Feature                                   | Page or component           | API contract                                              |
| ---- | ----------------------------------------- | --------------------------- | --------------------------------------------------------- |
| A.1  | Live basic race information               | Home, `RaceInfoSection`     | `GET /api/race`                                           |
| A.1  | Customer ticket purchase                  | `BuyTicket`                 | `GET /api/race`, `POST /api/tickets`                      |
| A.1  | Promo-code purchase                       | `BuyTicket`                 | `POST /api/tickets` with nullable `promoCode`             |
| A.1  | Ticket modification                       | `ManageTicket`              | `GET /api/race`, `PUT /api/tickets`                       |
| A.1  | Ticket cancellation                       | `ManageTicket`              | `POST /api/tickets/cancel?registrationCode=...&email=...` |
| A.1  | Paddock pass purchase                     | `PaddockPass`               | `POST /api/paddock-passes`                                |
| A.1  | Race administration                       | `Admin`                     | `GET /api/race`, `PUT /api/admin/race`                    |
| A.1  | Navigation and error states               | `Layout`, page-local states | No endpoint                                               |
| A.2  | Ticket count by competition day           | Reporting dashboard         | `GET /api/reports/tickets-by-race-day`                    |
| A.2  | Purchases by date and optional start date | Reporting dashboard         | `GET /api/reports/purchases-by-date?from=YYYY-MM-DD`      |
| A.2  | Paddock option counts                     | Reporting dashboard         | `GET /api/reports/paddock-passes`                         |
| A.2  | Eventual real-time refresh                | Reporting dashboard         | Polls all three report endpoints every 10 seconds         |

## A.1 Ticketing Application

### Public race information

The home route preserves the existing `LandingPageMain.jsx` hero, statistics, feature cards, CTA, and dark navy/slate/blue visual direction. Its visible ticket action goes to `/buy-ticket`; race/calendar calls-to-action scroll to the live race-information section. `RaceInfoSection` reads the active race and shows its name, location, dates, extra information, competition days, base prices, and capacity.

Every API-backed A.1 view shows loading and error feedback. The race-info, purchase, management, and administration views expose a Retry control when their configuration read fails.

### Ticket purchase

The purchase page requires first name, last name, address 1, postal code, city, country, email, and email confirmation. It obtains competition days, seating zones, allowed currencies, and the early-discount date from `GET /api/race`; it does not hardcode event data. A selected day produces exactly one seating-zone selection, preventing duplicate-day payload rows.

The request body is:

```json
{
  "customer": {
    "firstName": "...",
    "lastName": "...",
    "address1": "...",
    "postalCode": "...",
    "city": "...",
    "country": "...",
    "email": "...",
    "emailConfirmation": "..."
  },
  "days": [{ "date": "YYYY-MM-DD", "zone": "Zone name" }],
  "currency": "EUR",
  "promoCode": null
}
```

The client immediately rejects mismatched email addresses, no selected days, and selected days without a zone. The backend remains the authority for promo validity, seat availability, duplicate-day protection, allowed currency, early discount, and final currency conversion. A successful purchase replaces the form with the registration code, newly generated promo code, final amount, currency, and copy buttons. The registration code, not the database GUID, is the credential for later ticket actions.

### Ticket modification and cancellation

`ManageTicket` has separate Modify Ticket and Cancel Ticket sections. Modify accepts the registration code, original email, target currency, additions, and removals. It loads valid dates, zones, and currencies from the active race. There is no ticket lookup endpoint in the backend, so the removal list is explicitly described as dates the customer believes are on the ticket; the API validates that claim. The UI rejects an empty change and makes add/remove selections mutually exclusive per date.

The modify request is:

```json
{
  "registrationCode": "...",
  "email": "...",
  "addDays": [{ "date": "YYYY-MM-DD", "zone": "Zone name" }],
  "removeDays": ["YYYY-MM-DD"],
  "currency": "EUR"
}
```

After success, the returned total and currency are shown, and returned race-day/zone identifiers are resolved to readable values from the already loaded race configuration when possible.

Cancellation requires an explicit confirmation checkbox and posts URL-encoded query values. HTTP `204 No Content` is treated as success without JSON parsing. The form is cleared and the UI states that cancellation is final and invalidates the former promo code.

### Paddock pass

`PaddockPass` accepts a ticket registration code. Garage Access is selected and disabled at EUR 100. Pit Lane at EUR 50, Food at EUR 25, and Drinks/Beverages at EUR 25 are optional. The page computes only a simple EUR estimate. The ticketing service applies the early discount and derives the final currency from the active ticket, so there is no client-side currency selector or customer lookup.

The request body is:

```json
{
  "registrationCode": "...",
  "pitLane": false,
  "food": false,
  "drinks": false
}
```

The confirmation shows the returned pass identifier, final total, and selected options.

### Administration

`Admin` is open because this project has no user-account requirement. It preloads the current race and submits one full replacement request. The UI manages race days and seating zones as simple local row arrays and allowed currencies as comma-separated three-letter codes. It requires at least one day and one zone, validates unique day dates and zone names, validates nonnegative prices/surcharges, positive capacities, dates inside the race range, and at least one valid currency. Submitting is disabled while a request is running.

The request body is:

```json
{
  "name": "...",
  "location": "...",
  "startDate": "YYYY-MM-DD",
  "endDate": "YYYY-MM-DD",
  "additionalInformation": "...",
  "discountUntil": "YYYY-MM-DD",
  "raceDays": [{ "date": "YYYY-MM-DD", "basePrice": 0, "capacity": 1 }],
  "seatingZones": [
    { "name": "...", "characteristics": "...", "capacity": 1, "surcharge": 0 }
  ],
  "allowedCurrencies": ["EUR"]
}
```

## A.2 Reporting Application

The organizer portal is a separate Vite application running on port 5174. It does not import A.1 source files. It has one dashboard with three un-nested reporting sections, a manual Refresh button, visible last-refreshed time, initial loading feedback, request-error feedback, empty states, and a ten-second polling interval that is cleared on unmount.

Ticket-by-day and purchases-by-date are shown in accessible tables with small proportional CSS bars. Paddock data is shown as four compact statistics: total passes, Pit Lane, Food, and Drinks. The optional start-date field applies only to the purchases-by-date request and the Clear action restores the unfiltered report.

## Use Cases

### UC-01: View race information

**Actor:** Visitor. **Preconditions:** Ticketing API is reachable through the A.1 proxy. **Main flow:** The visitor opens Home; the page reads `GET /api/race` and renders race metadata and competition days. **Alternate/error flow:** The view displays loading, no-data, or an API error with Retry. **Postcondition:** No ticketing data is changed.

### UC-02: Purchase a ticket and optionally use a promo code

**Actor:** Customer. **Preconditions:** An active race has at least one available day, zone, and allowed currency. **Main flow:** The customer completes the required identity fields, chooses one or more days and zones, selects a currency, optionally enters a promo code, and submits `POST /api/tickets`. The backend calculates discounts/conversion and creates the ticket. **Alternate/error flow:** Email mismatch, incomplete zone choices, and no day are stopped in the browser; invalid promo, unavailable capacity, duplicate days, or invalid currency are shown from the backend. **Postcondition:** A ticket, registration code, and new promo code exist after successful persistence.

### UC-03: Modify a ticket

**Actor:** Customer. **Preconditions:** The customer has an active ticket, registration code, and original email. **Main flow:** The customer selects additions/removals and a currency, then submits `PUT /api/tickets`. **Alternate/error flow:** Empty changes and add/remove overlap are prevented; dates not actually on the ticket and other ticket rules are rejected by the API. **Postcondition:** The active ticket has an updated total, currency, and valid day set.

### UC-04: Cancel a ticket

**Actor:** Customer. **Preconditions:** The customer has a ticket registration code and original email. **Main flow:** The customer confirms the permanent cancellation and sends `POST /api/tickets/cancel`. **Alternate/error flow:** Missing confirmation or an unknown/already-cancelled ticket displays an error. **Postcondition:** The ticket becomes cancelled and its old promo code is invalid.

### UC-05: Purchase a paddock pass

**Actor:** Customer. **Preconditions:** The supplied registration code belongs to an active ticket without an existing paddock pass. **Main flow:** The customer keeps Garage Access, selects optional services, and sends `POST /api/paddock-passes`. **Alternate/error flow:** An inactive ticket or duplicate pass is rejected by the API. **Postcondition:** One paddock pass is linked to the ticket and its option choices are retained.

### UC-06: Edit race configuration

**Actor:** Administrator. **Preconditions:** The A.1 API is available. **Main flow:** The administrator loads the current configuration, updates race details/days/zones/currencies, and sends `PUT /api/admin/race`. **Alternate/error flow:** Browser validation blocks invalid arrays; backend validation messages remain visible. **Postcondition:** Race configuration is replaced and the ticketing race cache is refreshed by the backend.

### UC-07: View tickets by competition day

**Actor:** Organizer. **Preconditions:** Reporting events have reached the reporting database. **Main flow:** A.2 reads `GET /api/reports/tickets-by-race-day` and renders date/count rows. **Alternate/error flow:** Empty, loading, and failed reads are shown. **Postcondition:** No data is changed.

### UC-08: View purchases by date

**Actor:** Organizer. **Preconditions:** Reporting events have reached the reporting database. **Main flow:** The organizer optionally sets a start date, applies it, and A.2 reads `GET /api/reports/purchases-by-date?from=...`. **Alternate/error flow:** Clear removes the filter; no matching purchases produces an empty state. **Postcondition:** No data is changed; one ticket purchase is counted once even when it contains multiple race days.

### UC-09: View paddock pass uptake

**Actor:** Organizer. **Preconditions:** Paddock pass events have reached reporting. **Main flow:** A.2 reads `GET /api/reports/paddock-passes` and renders total, Pit Lane, Food, and Drinks counts. **Alternate/error flow:** Empty, loading, and failed reads are shown. **Postcondition:** No data is changed.

### UC-10: Convert a final ticket or paddock price

**Actor:** External Exchange Rate API. **Preconditions:** The ticketing service receives a non-EUR final-price request. **Main flow:** The ticketing backend requests the EUR-to-requested-currency rate from Frankfurter and calculates the total. **Alternate/error flow:** A missing exchange rate causes a backend request failure. **Postcondition:** The persisted total is in the selected ticket currency. The browser never calls Frankfurter directly.

### UC-11: Transfer reporting events

**Actor:** RabbitMQ. **Preconditions:** Ticketing persistence has committed an outbox record and RabbitMQ is configured. **Main flow:** The outbox worker publishes ticket/paddock events; the reporting consumer stores reporting projections. **Alternate/error flow:** Publication failure leaves the outbox event pending for retry; A.2 keeps polling its local reporting API. **Postcondition:** Reports update eventually, not necessarily during the originating request.

## Requirement Checklist

| Requirement                                                        | Status    | Notes                                                                                                                                                         |
| ------------------------------------------------------------------ | --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Preserve existing landing page and dark navy/slate/blue style      | Complete  | `LandingPageMain.jsx` retains its structure/classes; only route targets changed.                                                                              |
| A.1 routes, navigation, responsive layout, no authentication       | Complete  | Home, ticket purchase, management, paddock, administration, and 404 are present.                                                                              |
| A.1 development proxy on `5173` to ticketing API `5284`            | Complete  | Vite `/api` proxy.                                                                                                                                            |
| Live race information with retry/loading/error/empty states        | Complete  | Home `RaceInfoSection`.                                                                                                                                       |
| Ticket purchase fields, day/zone choice, currency, promo code      | Complete  | `BuyTicket`; final pricing remains backend-owned.                                                                                                             |
| Registration/promo code confirmation and copy controls             | Complete  | Purchase result replaces the form.                                                                                                                            |
| Ticket modify/cancel with HTTP 204 handling                        | Complete  | `ManageTicket`.                                                                                                                                               |
| Paddock options and EUR estimate                                   | Complete  | `PaddockPass`; no invented customer/currency lookup.                                                                                                          |
| Full race replacement administration                               | Complete  | `Admin` validates and sends race/days/zones/currencies.                                                                                                       |
| A.2 standalone app on `5174` to reporting API `5167`               | Complete  | Separate package, Vite config, and source tree.                                                                                                               |
| Three reports, optional start filter, Refresh, polling             | Complete  | Dashboard polls all report endpoints every 10 seconds.                                                                                                        |
| Mini-specification and UML sources                                 | Complete  | This document plus `docs/diagrams/*.puml`.                                                                                                                    |
| Logical data model and separate reporting database rationale       | Complete  | `data-model.md` and logical data model diagram.                                                                                                               |
| Document actual PostgreSQL + outbox + RabbitMQ flow                | Complete  | Sequence and class diagrams use the backend implementation.                                                                                                   |
| Exported PNG/SVG diagrams                                          | Limited   | PlantUML/Java renderer is not installed; sources are provided in `docs/diagrams`.                                                                             |
| Root `F:\boris\README.md` update                                   | Limited   | Not modified because the implementation scope was restricted to `F:\boris\frontend` and `F:\boris\docs`. Startup commands are documented in `docs/README.md`. |
| Existing backend source, migrations, entities, services, and tests | Preserved | No backend source changes were made.                                                                                                                          |

## Known Frontend-Only Limitations

1. The backend has no ticket lookup endpoint, so A.1 cannot prefill current customer data or current ticket selections before a modification or paddock purchase.
2. Paddock pass currency is intentionally not selectable in the browser; it is inherited by the ticketing backend from the ticket.
3. Reporting consumes original purchase events. The current consumer intentionally ignores later ticket modification/cancellation events, so reports reflect original purchase events as documented by the backend behavior.
4. Ticket persistence is PostgreSQL transaction plus outbox before RabbitMQ publication. It is not a queue-first purchase workflow.
