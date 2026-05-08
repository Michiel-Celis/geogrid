# Geogrid

A 2D (and eventually 3D) simulator that automatically subdivides land into plots using procedural,
organic-looking algorithms. Users draw a parcel boundary and (optionally) suggestive road lines,
and the system generates a road network plus plots that respect realistic planning rules.

See [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) for the milestone roadmap.

## Stack

- **Frontend**: React 19 + TypeScript + Vite
- **Backend**: ASP.NET Core (.NET 10) Web API + EF Core + NetTopologySuite
- **Database**: PostgreSQL 16 + PostGIS 3.4

## Repository layout

```
api/        .NET solution (Geogrid.Api, .Domain, .Infrastructure, .Tests)
web/        Vite React TS app
db/         SQL migrations / seed data (placeholder)
docker/     Dockerfiles (api, web)
.github/    CI workflows
```

## Prerequisites

- .NET 10 SDK
- Node.js 22+
- Docker Desktop (for PostGIS)

## Getting started

### 1. Start PostGIS

```powershell
docker compose up -d db
```

### 2. Run the API

```powershell
cd api
dotnet run --project src/Geogrid.Api
```

API listens on http://localhost:5000. Health check: http://localhost:5000/api/health

### 3. Run the web app

```powershell
cd web
npm install
npm run dev
```

Web listens on http://localhost:5173.

### 4. (Optional) Run everything in Docker

```powershell
docker compose --profile full up --build
```

## Tests

```powershell
cd api
dotnet test
```

```powershell
cd web
npm run lint
npm run build
```
