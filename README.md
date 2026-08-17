# Premier ERP — .NET 8 Backend

ASP.NET Core 8 Web API that serves `premier-erp.html` and provides authentication,
per-module CRUD, offline sync, and an audit trail for every ERP collection
(CRM incl. Service Catalogue & Quotations, Inventory, Procurement, Fixed Assets,
HR, Compliance incl. API Q2 Clause Matrix, Accounting blob, Inspection report library).

## Projects & layout
```
PremierErp.Api/
  Program.cs                 host, JWT auth, CORS, DB migrate + admin seed, static hosting
  Models/Entities.cs         AppUser, ErpRecord (generic JSON record), SyncBlob, AuditEntry
  Data/ErpDbContext.cs       EF Core context (SQLite dev / SQL Server & Azure SQL prod)
  Services/TokenService.cs   JWT issuing
  Controllers/
    AuthController.cs        POST /api/auth/login | register (admin) | change-password | GET me
    RecordsController.cs     GET/POST /api/records/{collection}, DELETE /api/records/{c}/{id}
    SyncController.cs        POST /api/sync/push, GET /api/sync/pull?since=...
    BlobsController.cs       GET/PUT /api/blobs/{key}   (gl, ir_library, ...)
    DashboardController.cs   GET /api/dashboard         (counts, open NCRs, overdue cals)
    HealthController.cs      GET /api/health
  wwwroot/premier-erp.html   the ERP front end (served at site root)
  wwwroot/erp-api-client.js  drop-in sync client for the HTML app
  web.config                 IIS / Azure App Service (Windows)
  Dockerfile                 container deployment (Linux App Service / ACA / AKS)
```

## Run locally
```bash
dotnet restore
dotnet run          # http://localhost:5000  (Swagger at /swagger)
```
Default admin (change immediately): configured in `appsettings.json` → `Seed`.

## Database
- Dev default: SQLite (`premier_erp.db`, zero setup).
- Production: set `Database:Provider` to `SqlServer` and put your Azure SQL /
  SQL Server connection string in `ConnectionStrings:Default`
  (see `appsettings.Production.json`). Tables are created automatically on first run.

## Deploy to Azure App Service (recommended)
1. Create an App Service (.NET 8, Windows or Linux) and an Azure SQL database.
2. `dotnet publish -c Release -o publish`
3. Zip-deploy: `az webapp deploy --resource-group RG --name APP --src-path publish.zip --type zip`
   (or push the folder from Visual Studio / VS Code Azure extension).
4. In App Service → Configuration, set:
   - `ConnectionStrings__Default` = your Azure SQL connection string
   - `Database__Provider` = `SqlServer`
   - `Jwt__Key` = long random secret (48+ chars)
   - `Seed__AdminEmail` / `Seed__AdminPassword` = first admin login
5. Browse the site — the ERP loads at `/`, API at `/api/...`, Swagger at `/swagger`.

## Deploy with Docker (Linux App Service / Container Apps / any VPS)
```bash
docker build -t premier-erp .
docker run -p 8080:8080 \
  -e ConnectionStrings__Default="Server=...;Database=PremierErp;..." \
  -e Database__Provider=SqlServer \
  -e Jwt__Key="LONG-RANDOM-SECRET" \
  premier-erp
```

## Deploy to IIS on a Windows Server VM
1. Install the .NET 8 Hosting Bundle.
2. `dotnet publish -c Release -o C:\inetpub\premier-erp`
3. Create an IIS site pointing at that folder (the included `web.config` is used).
4. Set environment variables or edit `appsettings.Production.json` for the DB + JWT key.

## Wiring the HTML app to the backend
The served copy at `/` is same-origin, so in the browser console (or a small UI you
can add later):
```js
erpApi.login('za.khan@premiertubular.com', '...').then(() => erpApi.sync());
```
- `erpApi.sync()` pushes all local records/blobs, then pulls server changes and re-renders.
- Sync is last-write-wins per record; deletions propagate as tombstones.
- Working offline is unchanged — everything stays in localStorage and syncs when back online.

## Security notes
- Change the seeded admin password immediately (`/api/auth/change-password`).
- Set a unique `Jwt:Key` in production; restrict `Cors:Origins` to your domain.
- Roles: `admin` (user management), `manager`, `user` (write), `readonly` (no writes).
- Every write is captured in the `Audit` table (who / what / when).
