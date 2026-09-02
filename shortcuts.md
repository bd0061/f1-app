# Shortcuts

## Servisi i URL-ovi

| Servis | URL | Beleška |
|---|---|---|
| A.1 Ticketing Frontend | http://localhost:5173 | |
| A.1 Ticketing API / Swagger | http://localhost:5284/swagger | |
| A.2 Reporting Frontend | http://localhost:5174 | |
| A.2 Reporting API / Swagger | http://localhost:5167/swagger | |
| RabbitMQ Management | http://localhost:15672 | login: `f1` / `f1` |
| PostgreSQL (A.1) | `localhost:55432` | db `f1`, user `f1` / `f1` |
| PostgreSQL (A.2 reporting) | `localhost:5433` | db `f1_reporting`, user `f1` / `f1` |
| Redis | `localhost:6379` | |
| pgAdmin | http://localhost:5050 | login: `admin@admin.com` / `admin` |

## Nazivi servisa (docker-compose.yml)

```
postgres
reporting-postgres
redis
rabbitmq
ticketing-api
reporting-api
ticketing-frontend
reporting-frontend
pgadmin
```

## Docker compose shortcuts

### Podizanje

```bash
docker compose up -d --build              # svi servisi
docker compose up -d --build <naziv>      # pojedinačni servis
```

### Gašenje

```bash
docker compose down                       # svi servisi
docker compose down <naziv>               # pojedinačni servis
```

### Stop / start direktno preko naziva kontejnera

```bash
sudo docker stop <naziv-servisa>
sudo docker start <naziv-servisa>
```

### Logovi

```bash
docker compose logs -f <naziv>
```
