# Documentation

## Contents

- [Mini specification](mini-specification.md)
- [Logical data model](data-model.md)
- [Use-case diagram source](diagrams/use-case.puml)
- [Logical data model diagram source](diagrams/logical-data-model.puml)
- [Ticket purchase sequence diagram source](diagrams/ticket-purchase-sequence.puml)
- [Final class diagram source](diagrams/final-class-diagram.puml)

## Running the Applications

Start infrastructure from `F:\boris\backend`:

```powershell
docker compose up -d
```

Run the ticketing API on its configured `http://localhost:5284` target:

```powershell
dotnet run --project F:\boris\backend\F1Ticketing.Api
```

Run the reporting API on its configured `http://localhost:5167` target:

```powershell
dotnet run --project F:\boris\backend\F1Reporting.Api
```

Run A.1 from `F:\boris\frontend\testing`:

```powershell
npm install
npm run dev
```

Open `http://localhost:5173`. Its Vite development proxy sends `/api/*` to `http://localhost:5284`.

Run A.2 from `F:\boris\frontend\reporting`:

```powershell
npm install
npm run dev
```

Open `http://localhost:5174`. Its Vite development proxy sends `/api/*` to `http://localhost:5167`.

RabbitMQ delivery and the reporting consumer are eventual. The reporting dashboard refreshes every 10 seconds, so new ticket or paddock data can appear shortly after an A.1 purchase succeeds.

## Rendering Diagrams

Install PlantUML with a Java runtime, then run this command from `F:\boris\docs`:

```powershell
plantuml -tsvg diagrams\use-case.puml diagrams\logical-data-model.puml diagrams\ticket-purchase-sequence.puml diagrams\final-class-diagram.puml -o rendered
```

Use `-tpng` instead of `-tsvg` to generate PNG output. The expected export location is `docs/diagrams/rendered`. No renderer was available during implementation, so this directory is intentionally empty until a PlantUML renderer is installed.
