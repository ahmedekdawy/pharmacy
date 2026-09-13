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

1. Update connection string in `backend/src/Pharmacy.Api/appsettings.Development.json`.
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

| Workflow | File | FTP folder |
|---|---|---|
| Frontend | `.github/workflows/deploy-frontend.yml` | `./` (site root) |
| Backend | `.github/workflows/deploy-backend.yml` | `./api/` |

Triggers: push to `master` (path-filtered), pull request (build only), or manual `workflow_dispatch`.

Frontend sync **excludes** `api/**` so Angular deploys never wipe the API folder. SPA `web.config` already skips rewriting `/api` routes.

### GitHub Environments & secrets

Create environments:

- `production-backend`
- `production-frontend`

**Shared FTP secrets** (same values on both environments, or repo secrets):

| Secret | Example |
|---|---|
| `FTP_SERVER` | `ftp.runasp.net` (or host from control panel) |
| `FTP_USERNAME` | your FTP user |
| `FTP_PASSWORD` | your FTP password |

**Optional variables**

| Variable | Default | Purpose |
|---|---|---|
| `FTP_SERVER_DIR` | `./` | Frontend (site root) folder |
| `FTP_API_DIR` | `./api/` | API subfolder on the same FTP |

**Backend-only secrets** (`production-backend`)

| Secret | Purpose |
|---|---|
| `DATABASE_CONNECTION_STRING` | Written into `appsettings.Production.json` before upload |
| `JWT_KEY` | Production JWT signing key |

**Frontend-only secrets** (`production-frontend`)

| Secret | Purpose |
|---|---|
| `API_BASE_URL` | Optional. Default production build uses `/api/v1` (same host). Set only if the API URL differs. |

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
