# Pharmacy

Multi-tenant pharmacy management platform (ASP.NET Core + Angular + PostgreSQL).

## Structure

```text
/pharmacy
  /backend          .NET modular monolith (CQRS)
  /frontend         Angular SPA
  Pharmacy_Technical_Foundation.md
```

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- PostgreSQL

## Backend

1. Configure your connection string with .NET user-secrets (keeps credentials out of git):

```bash
cd backend/src/Pharmacy.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Username=...;Password=..."
```
2. Apply migrations:

```bash
cd backend
dotnet ef database update --project src/Pharmacy.Infrastructure --startup-project src/Pharmacy.Api
```

3. Run API:

```bash
cd backend/src/Pharmacy.Api
dotnet run
```

Health check: `GET /health`

Products API (requires tenant header):

```http
GET /api/v1/products
X-Tenant-Id: {tenant-guid}
```

## Frontend

```bash
cd frontend
npm start
```

Open `http://localhost:4200`

Login stores `tenant_id` and sends `X-Tenant-Id` on API calls.

## Auth (demo seed)

On API startup the seeder creates:

- Tenant code: `DEMO`
- Admin: `admin@demo.pharmacy` / `Admin@123`
- Owner role with all permissions and pages

Login: `POST /api/v1/auth/login`

```json
{ "tenantCode": "DEMO", "email": "admin@demo.pharmacy", "password": "Admin@123" }
```

Users can be activated/deactivated via:

- `POST /api/v1/users/{id}/activate`
- `POST /api/v1/users/{id}/deactivate`

## Deploy pipelines (FTP → runasp.net)

Frontend and API share **one FTP account**. Layout on the server:

```text
./                 ← Angular (site root)
./api/             ← ASP.NET Core API
```

| Workflow | File | FTP host | FTP folder |
|---|---|---|---|
| Frontend | `.github/workflows/deploy-frontend.yml` | frontend site (`FRONTEND_FTP_*`) | `./wwwroot/` (site root) |
| Backend | `.github/workflows/deploy-backend.yml` | API site (`FTP_*`) | `./wwwroot/api/` |

Triggers: push to `master` (path-filtered), pull request (build only), or manual `workflow_dispatch`.

Frontend and backend deploy to **different FTP sites**, so each pipeline only touches its own host.

### GitHub Environments & secrets

Create environments:

- `production-backend`
- `production-frontend`

**Backend secrets** (`production-backend`) — API FTP site

| Secret | Purpose |
|---|---|
| `FTP_SERVER` | API site FTP host (from runasp.net control panel) |
| `FTP_USERNAME` | API site FTP user |
| `FTP_PASSWORD` | API site FTP password |
| `DATABASE_CONNECTION_STRING` | Written into `appsettings.Production.json` before upload |
| `JWT_KEY` | Production JWT signing key |

**Frontend secrets** (`production-frontend`) — frontend FTP site

| Secret | Purpose |
|---|---|
| `FRONTEND_FTP_SERVER` | Frontend site FTP host |
| `FRONTEND_FTP_USERNAME` | Frontend site FTP user |
| `FRONTEND_FTP_PASSWORD` | Frontend site FTP password |
| `API_BASE_URL` | Optional. Defaults to `https://pharmacyegapi.runasp.net/api/v1`. |

**Optional variables**

| Variable | Default | Purpose |
|---|---|---|
| `FRONTEND_FTP_SERVER_DIR` | `./wwwroot/` | Frontend folder on the frontend FTP |
| `FTP_API_DIR` | `./wwwroot/api/` | API folder on the backend FTP |

### runasp.net tips

1. Create an `api` folder under the site (first backend deploy can create it).
2. In the hosting panel, convert `/api` to an **IIS Application** (ASP.NET Core) if required.
3. Enable ASP.NET Core / hosting bundle support for that application.
4. Frontend root `web.config` rewrites Angular routes and leaves `/api` alone.
5. API uses `PathBase=/api` with routes `v1/...`, so the public URL is still `https://your-site/api/v1/...`.
6. Smoke-test `https://your-site/api/health`, then the site root.

## Foundation progress (§60)

Done: Users/Permissions, Products, Categories, Brands, Locations, Inventory (opening balance, transfer, adjust, low stock, near expiry), Suppliers, Purchases, Customers, Sales/POS, Sale returns, Cash shifts, Expenses, Sales + Profit reports, Dashboard, Audit logs, Tenant settings, Egyptian drug catalog search, FTP deploy pipelines.

Next (future §61): Smart reordering, purchase returns depth, richer POS UI.

## Standards baked in

- Shared DB / shared schema multi-tenancy via `tenant_id`
- EF global query filters for tenant + soft delete
- PostgreSQL tables/columns are lowercase `snake_case`
- CQRS with MediatR + FluentValidation
- Angular Reactive Forms and bilingual EN/AR scaffolding
