# F1 Specifikacija za prodaju karata i izveštavanje

## Opseg

Projekat sadrži dve nezavisne React/Vite jednostranične aplikacije. A.1 je javna aplikacija za prodaju karata na `frontend/testing`; A.2 je portal za izveštavanje organizatora na `frontend/reporting`. Obe aplikacije koriste Vite razvojni proxy i održavaju zahteve iz browsera na `/api`. Backend za prodaju karata upravlja cenama, konverzijom valuta, Redis keširanjem, PostgreSQL perzistencijom, outbox-om i objavljivanjem na RabbitMQ. Backend za izveštavanje poseduje svoju zasebnu bazu podataka za izveštavanje i konzumira RabbitMQ događaje.

Frontend aplikacije namerno koriste lokalno React stanje stranice, `useEffect`, direktan `fetch` i običan CSS. Ne koriste sistem naloga, klijentsku integraciju kursa valuta, zajednički frontend paket, biblioteku za grafikone, Redux niti biblioteku za forme.

## Mapa funkcionalnosti

| Oblast | Funkcionalnost | Stranica ili komponenta | API ugovor |
| ---- | ----------------------------------------- | --------------------------- | --------------------------------------------------------- |
| A.1 | Prikaz aktuelnih osnovnih informacija o trci | Home, `RaceInfoSection` | `GET /api/race` |
| A.1 | Kupovina karte za korisnika | `BuyTicket` | `GET /api/race`, `POST /api/tickets` |
| A.1 | Kupovina sa promo kodom | `BuyTicket` | `POST /api/tickets` sa nullable `promoCode` |
| A.1 | Izmena karte | `ManageTicket` | `GET /api/race`, `PUT /api/tickets` |
| A.1 | Otkazivanje karte | `ManageTicket` | `POST /api/tickets/cancel?registrationCode=...&email=...` |
| A.1 | Kupovina paddock propusnice | `PaddockPass` | `POST /api/paddock-passes` |
| A.1 | Administracija trke | `Admin` | `GET /api/race`, `PUT /api/admin/race` |
| A.1 | Navigacija i stanja greške | `Layout`, lokalna stanja stranica | Nema endpoint-a |
| A.2 | Broj karata po danu takmičenja | Reporting dashboard | `GET /api/reports/tickets-by-race-day` |
| A.2 | Kupovine po datumu i opcionom početnom datumu | Reporting dashboard | `GET /api/reports/purchases-by-date?from=YYYY-MM-DD` |
| A.2 | Broj paddock opcija | Reporting dashboard | `GET /api/reports/paddock-passes` |
| A.2 | Eventualno osvežavanje u realnom vremenu | Reporting dashboard | Poziva sva tri report endpoint-a na svakih 10 sekundi |

## A.1 Aplikacija za prodaju karata

### Javne informacije o trci

Home ruta zadržava postojeći hero deo iz `LandingPageMain.jsx`, statistike, kartice funkcionalnosti, CTA i tamnu navy/slate/plavu vizuelnu usmerenost. Vidljiva akcija za kupovinu karte vodi na `/buy-ticket`; CTA dugmad za trku/kalendarske informacije skroluju do sekcije sa aktuelnim informacijama o trci. `RaceInfoSection` učitava aktivnu trku i prikazuje njen naziv, lokaciju, datume, dodatne informacije, dane takmičenja, osnovne cene i kapacitet.

Svaki A.1 prikaz koji koristi API prikazuje stanje učitavanja i greške. Prikazi informacija o trci, kupovine, upravljanja kartom i administracije nude kontrolu Retry kada učitavanje konfiguracije ne uspe.

### Kupovina karte

Stranica za kupovinu zahteva ime, prezime, adresu 1, poštanski broj, grad, državu, email i potvrdu email adrese. Podatke o danima takmičenja, zonama za sedenje, dozvoljenim valutama i datumu ranog popusta dobija putem `GET /api/race`; podaci o događaju nisu hardkodovani. Izbor jednog dana proizvodi tačno jedan izbor zone za sedenje, čime se sprečavaju duplirani redovi u payload-u za isti dan.

Telo zahteva je:

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