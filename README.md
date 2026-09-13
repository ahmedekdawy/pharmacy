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

## Foundation progress (§60)

Done: Users/Permissions, Products, Categories, Brands, Locations, Inventory, Suppliers, Purchases (receive), Customers, Sales/POS, Sale returns, Cash shifts, Expenses, Sales reports, Tenant settings, Audit log table, app shell nav.

Next: Sale purchase returns depth, profit reports, transfers, richer POS UI.

## Standards baked in

- Shared DB / shared schema multi-tenancy via `tenant_id`
- EF global query filters for tenant + soft delete
- PostgreSQL tables/columns are lowercase `snake_case`
- CQRS with MediatR + FluentValidation
- Angular Reactive Forms and bilingual EN/AR scaffolding
