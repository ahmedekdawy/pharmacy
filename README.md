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

Two independent GitHub Actions workflows:

| Workflow | File | Deploys |
|---|---|---|
| Backend | `.github/workflows/deploy-backend.yml` | Published ASP.NET Core API via FTP |
| Frontend | `.github/workflows/deploy-frontend.yml` | Angular static site via FTP |

Triggers: push to `master` (path-filtered), pull request (build only), or manual `workflow_dispatch`.

### GitHub Environments & secrets

Create environments:

- `production-backend`
- `production-frontend`

**Shared FTP secrets** (set on each environment, or repo secrets):

| Secret | Example |
|---|---|
| `FTP_SERVER` | `ftp.runasp.net` (or host from control panel) |
| `FTP_USERNAME` | your FTP user |
| `FTP_PASSWORD` | your FTP password |

**Optional variable**

| Variable | Example |
|---|---|
| `FTP_SERVER_DIR` | `./` or `/site1/` (folder on FTP for that app) |

**Backend-only secrets** (`production-backend`)

| Secret | Purpose |
|---|---|
| `DATABASE_CONNECTION_STRING` | Written into `appsettings.Production.json` before upload |
| `JWT_KEY` | Production JWT signing key |

**Frontend-only secrets** (`production-frontend`)

| Secret | Purpose |
|---|---|
| `API_BASE_URL` | e.g. `https://api.yourdomain.com/api/v1` (injected at build time) |

### runasp.net tips

1. Point each site/app to its own FTP folder (`FTP_SERVER_DIR`).
2. Backend: enable ASP.NET Core / install hosting bundle support in the panel; upload goes to the site root (often `wwwroot` / site folder).
3. Frontend: `web.config` is included for Angular route rewrite on IIS.
4. If API and UI are on different hosts, set `API_BASE_URL` for the frontend pipeline.

## Foundation progress (§60)

Done: Users/Permissions, Products, Categories, Brands, Locations, Inventory, Suppliers, Purchases (receive), Customers, Sales/POS, Sale returns, Cash shifts, Expenses, Sales reports, Tenant settings, Audit log table, app shell nav.

Next: Sale purchase returns depth, profit reports, transfers, richer POS UI.

## Standards baked in

- Shared DB / shared schema multi-tenancy via `tenant_id`
- EF global query filters for tenant + soft delete
- PostgreSQL tables/columns are lowercase `snake_case`
- CQRS with MediatR + FluentValidation
- Angular Reactive Forms and bilingual EN/AR scaffolding
