# Pokretanje Celog Sistema

Otvori **pet PowerShell terminala**. Prvi terminal pokreće infrastrukturu, sledeća dva .NET API-je, a poslednja dva React/Vite aplikacije. Terminali za API-je i frontend ostaju pokrenuti dok testiraš aplikaciju.

## Preduslovi

- Docker Desktop je pokrenut.
- Instaliran je .NET SDK.
- Instaliran je Node.js i npm.

## Terminal 1: Infrastruktura

Ova komanda pokreće PostgreSQL baze, Redis i RabbitMQ iz `docker-compose.yml` fajla.

```powershell
Set-Location "F:\boris\backend"
docker compose up -d
docker compose ps
```

Sačekaj da `docker compose ps` prikaže pokrenute kontejnere pre nego što pokreneš API-je.

## Terminal 2: Ticketing API (A.1 backend)

Ticketing API sluša na portu `5284`. Pri pokretanju primenjuje migracije i, kada je baza prazna, dodaje Monaco primer trke.

```powershell
dotnet run --project "F:\boris\backend\F1Ticketing.Api" -- --urls "http://localhost:5284"
```

Swagger je dostupan na [http://localhost:5284/swagger](http://localhost:5284/swagger).

## Terminal 3: Reporting API (A.2 backend)

Reporting API sluša na portu `5167` i u pozadini pokreće RabbitMQ consumer za reporting bazu.

```powershell
dotnet run --project "F:\boris\backend\F1Reporting.Api" -- --urls "http://localhost:5167"
```

Swagger je dostupan na [http://localhost:5167/swagger](http://localhost:5167/swagger).

## Terminal 4: A.1 Ticketing Frontend

Prvi put instaliraj pakete. Posle toga je dovoljna samo `npm run dev` komanda.

```powershell
Set-Location "F:\boris\frontend\testing"
npm install
npm run dev
```

Otvori [http://localhost:5173](http://localhost:5173). Vite proxy automatski šalje svaki `/api/*` zahtev na `http://localhost:5284`, pa ne treba ručno podešavati CORS.

## Terminal 5: A.2 Reporting Frontend

Prvi put instaliraj pakete. Posle toga je dovoljna samo `npm run dev` komanda.

```powershell
Set-Location "F:\boris\frontend\reporting"
npm install
npm run dev
```

Otvori [http://localhost:5174](http://localhost:5174). Vite proxy automatski šalje svaki `/api/*` zahtev na `http://localhost:5167`.

## Redosled Provere

1. Otvori A.1 na `http://localhost:5173` i proveri da se učitava race information bez `502 Bad Gateway` poruke.
2. Kupi kartu u A.1 i sačuvaj registration code i promo code.
3. Sačekaj najmanje 10 sekundi, zatim otvori ili osveži A.2 na `http://localhost:5174`.
4. Proveri da se ticket pojavljuje u izveštajima po danu trke i po datumu kupovine.
5. Kupi paddock pass u A.1, zatim nakon sledećeg A.2 osvežavanja proveri paddock statistiku.

Reporting podaci stižu asinhrono: ticketing API najpre čuva podatke i outbox poruku u PostgreSQL, zatim worker šalje događaj kroz RabbitMQ, a reporting consumer ažurira svoju bazu. Zato novi podaci ne moraju biti vidljivi istog trenutka kada A.1 prikaže potvrdu kupovine.

## Zaustavljanje

U terminalima 2-5 pritisni `Ctrl+C` da zaustaviš API-je i Vite servere. Zatim iz terminala 1 ugasi infrastrukturu:

```powershell
Set-Location "F:\boris\backend"
docker compose down
```

Za logove infrastrukture koristi:

```powershell
Set-Location "F:\boris\backend"
docker compose logs -f
```

## Brzo Otklanjanje Problema

| Simptom                                | Provera                                                                                                  |
| -------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| A.1 ili A.2 pokazuje `502 Bad Gateway` | Proveri da li odgovarajući .NET API terminal radi na portu `5284` ili `5167`.                            |
| Ticketing API ne može da se poveže     | Pokreni `docker compose ps` i proveri PostgreSQL i Redis kontejnere.                                     |
| Reporting ostaje prazan                | Proveri RabbitMQ kontejner, Reporting API terminal i sačekaj sledeći interval osvežavanja od 10 sekundi. |
| Port je već zauzet                     | Zaustavi prethodni proces na tom portu ili prvo proveri da li je aplikacija već pokrenuta.               |
