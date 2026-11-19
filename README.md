# ConsignmentConsole

## Local development

### Prerequisites

- Docker Desktop with Docker Compose
- .NET 8 SDK
- Node.js and npm

### Start PostgreSQL

From the repository root, create a local environment file once and replace its placeholder password with a local-development-only value:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
```

`.env` is gitignored. Do not commit it or use its password outside local development.

Start PostgreSQL and wait for its health check:

```powershell
docker compose up -d db
docker compose ps
```

The database is `consignments`, the user is `app`, and it is available only through `localhost:5432`. Its data is kept in the `postgres-data` named volume.

Stop PostgreSQL without deleting its data:

```powershell
docker compose stop db
```

Start it again with `docker compose up -d db`. Do not use `docker compose down --volumes` if you want to retain the database.

### Apply migrations and start the backend

In a PowerShell terminal at the repository root, load the local database password into the connection string and apply the existing EF Core migrations:

```powershell
$password = (Get-Content .env | Where-Object { $_ -match '^POSTGRES_PASSWORD=' }) -replace '^POSTGRES_PASSWORD=', ''
$env:ConnectionStrings__ConsignmentDatabase = "Host=localhost;Port=5432;Database=consignments;Username=app;Password=$password"
dotnet ef database update --project src/Api --startup-project src/Api
```

In that same terminal, start the API:

```powershell
dotnet run --project src/Api --launch-profile http
```

The API listens at `http://localhost:5020`. It imports the root `consignments.csv` during startup; check the API log for the import totals. In another terminal, confirm the report is available:

```powershell
Invoke-RestMethod http://localhost:5020/api/imports/latest
```

The connection string is supplied via `ConnectionStrings__ConsignmentDatabase` and overrides local appsettings. Keep the environment variable scoped to the terminal running EF or the API.

### Start the frontend

In another terminal:

```powershell
Set-Location web
npm run dev
```

Open the Vite URL printed in the terminal. The frontend development proxy targets the API at `http://localhost:5020`.

