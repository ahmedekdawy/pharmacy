# Pharmacy Management System
## Technical Foundation & Project Standards

**Project Name:** `Pharmacy`

This document defines the main technology stack, architecture standards, project structure, coding rules, database strategy, frontend standards, multi-tenancy approach, localization requirements, and performance guidelines for the Pharmacy Management System.

---

# 1. Project Goals

The system should be designed as a modern, high-performance, multi-tenant pharmacy management platform that can support:

- Single pharmacy
- Multiple pharmacy branches
- Warehouses
- Multiple organizations / tenants
- Arabic and English
- Daily pharmacy operations
- Future SaaS deployment
- High transaction volume
- Clean separation between frontend and backend
- Easy maintenance and future scaling

The first version should remain simple operationally while the architecture must allow future growth without major redesign.

---

# 2. Main Technology Stack

## Backend

Use:

- `.NET Core / ASP.NET Core`
- Latest stable supported .NET version
- `C#`
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- CQRS
- Dependency Injection
- FluentValidation
- Swagger / OpenAPI
- Structured logging
- Global exception handling
- API versioning

Preferred backend architecture:

```text
ASP.NET Core Web API
        |
        v
Application Layer
        |
        v
Domain Layer
        |
        v
Infrastructure Layer
        |
        v
PostgreSQL
```

---

# 3. Frontend

Frontend must be implemented as a completely separate project from the backend.

Use:

- Angular
- TypeScript
- Angular Router
- Angular HttpClient
- RxJS
- Angular Reactive Forms
- Angular Interceptors
- Lazy-loaded feature modules or standalone feature routes
- Responsive design
- RTL / LTR support
- Arabic / English localization

Example repositories:

```text
pharmacy-backend
pharmacy-frontend
```

Or:

```text
/pharmacy
    /backend
    /frontend
```

Backend and frontend must be independently buildable and deployable.

---

# 4. Angular Form Standard

Always use:

```text
Reactive Forms
```

Do not use template-driven forms for business forms.

Use:

```typescript
FormGroup
FormControl
FormArray
FormBuilder
Validators
```

All business forms should support:

- Client-side validation
- Backend validation messages
- Disabled states
- Loading states
- Submit protection
- Validation message translation
- Reusable form controls when appropriate

Example:

```typescript
this.form = this.fb.group({
  nameAr: ['', Validators.required],
  nameEn: ['', Validators.required],
  code: ['', Validators.required],
  isActive: [true]
});
```

---

# 5. Architecture Style

Start with a:

```text
Modular Monolith
```

Do not start with microservices.

Modules should remain logically separated so they can be extracted into services later if genuinely required.

Possible modules:

```text
Identity
Tenants
Branches
Warehouses
Catalog
Inventory
Purchasing
Sales
Suppliers
Customers
CashManagement
Expenses
Reporting
Notifications
Audit
Settings
```

---

# 6. Recommended Backend Solution Structure

```text
Pharmacy.sln

src/
|
|-- Pharmacy.Api
|
|-- Pharmacy.Application
|
|-- Pharmacy.Domain
|
|-- Pharmacy.Infrastructure
|
|-- Pharmacy.Contracts
|
|-- Pharmacy.Shared
|
tests/
|
|-- Pharmacy.UnitTests
|
|-- Pharmacy.IntegrationTests
```

---

# 7. Layer Responsibilities

## Pharmacy.Api

Responsible for:

- Controllers / endpoints
- Authentication
- Authorization
- Middleware
- Swagger
- API versioning
- Request / response handling
- Dependency injection registration

The API layer should contain minimum business logic.

---

## Pharmacy.Application

Responsible for:

- CQRS commands
- CQRS queries
- Handlers
- DTOs
- Validators
- Application services
- Mapping
- Business use cases
- Interfaces

Example:

```text
Products/
    Commands/
        CreateProduct
        UpdateProduct
        DeleteProduct

    Queries/
        GetProductById
        GetProducts
        SearchProducts
```

---

## Pharmacy.Domain

Responsible for:

- Entities
- Value Objects
- Domain rules
- Domain events
- Enumerations
- Business logic

The Domain project should not depend on Entity Framework Core or ASP.NET Core.

---

## Pharmacy.Infrastructure

Responsible for:

- PostgreSQL
- Entity Framework Core
- DbContext
- EF configurations
- Migrations
- Repositories where needed
- External integrations
- Caching
- File storage
- Email / SMS integrations
- Logging infrastructure

---

# 8. CQRS Standard

Use CQRS for application operations.

Separate:

```text
Commands
```

from:

```text
Queries
```

Commands modify system state.

Examples:

```text
CreateProductCommand
UpdateProductCommand
ReceivePurchaseCommand
CreateSaleCommand
TransferStockCommand
CloseCashShiftCommand
```

Queries retrieve data.

Examples:

```text
GetProductQuery
SearchProductsQuery
GetStockQuery
GetDailySalesQuery
GetLowStockQuery
GetNearExpiryProductsQuery
```

CQRS does not require separate databases.

Use the same PostgreSQL database initially.

---

# 9. Entity Framework Core

Use Entity Framework Core as the primary ORM.

Database provider:

```text
Npgsql.EntityFrameworkCore.PostgreSQL
```

Use:

- Fluent API configuration
- IEntityTypeConfiguration<T>
- Explicit indexes
- Explicit constraints
- Proper relationship mappings
- AsNoTracking for read-only queries
- Projection instead of loading unnecessary entities
- Pagination for large datasets
- Explicit lowercase `snake_case` table and column name mappings

Every entity configuration must map to lowercase PostgreSQL identifiers, for example `ToTable("products")` and `HasColumnName("tenant_id")`.

Avoid putting large EF configurations inside `OnModelCreating`.

Use:

```text
Configurations/
    ProductConfiguration.cs
    ProductBatchConfiguration.cs
    SaleConfiguration.cs
    SaleItemConfiguration.cs
```

---

# 10. PostgreSQL Database

Use PostgreSQL as the primary relational database.

Database naming example:

```text
pharmacy
```

Development:

```text
pharmacy_dev
```

Testing:

```text
pharmacy_test
```

Production may follow:

```text
pharmacy_prod
```

---

# 11. Database Migration Strategy

Create and maintain database schema using Entity Framework Core migrations.

Do not manually modify the production database schema unless handling a controlled emergency.

Create migrations from the Infrastructure project.

Example:

```bash
dotnet ef migrations add InitialCreate \
  --project src/Pharmacy.Infrastructure \
  --startup-project src/Pharmacy.Api
```

Apply migration:

```bash
dotnet ef database update \
  --project src/Pharmacy.Infrastructure \
  --startup-project src/Pharmacy.Api
```

Recommended migration folder:

```text
Pharmacy.Infrastructure/
    Persistence/
        Migrations/
```

Migration names should clearly describe changes.

Examples:

```text
InitialCreate
AddProductBatch
AddWarehouseTransfer
AddSupplierBalance
AddTenantSettings
```

Avoid migration names such as:

```text
Update1
Fix
Changes
TestMigration
```

---

# 12. Database Design Standards

All PostgreSQL tables and columns must use lowercase `snake_case` names only.

All major tenant-owned tables should normally contain:

```text
id
tenant_id
created_at
created_by
updated_at
updated_by
is_deleted
```

`tenant_id` is required on every tenant-owned table and must participate in keys and indexes used for tenant isolation (see Multi-Tenancy).

Use UTC for server-side timestamps.

Prefer:

```text
timestamptz
```

in PostgreSQL.

---

# 13. Primary Keys

Prefer:

```text
UUID / Guid
```

for business entities.

Example:

```csharp
public Guid Id { get; set; }
```

Benefits include:

- Easier distributed creation
- Safer multi-tenant identifiers
- Easier future synchronization
- Less predictable public IDs

---

# 14. Multi-Tenancy

The platform must be designed as multi-tenant from the beginning.

A tenant represents one pharmacy business / organization.

Example:

```text
Tenant A
    Branch 1
    Branch 2
    Warehouse

Tenant B
    Branch 1
    Warehouse
```

Tenant data must never be visible to another tenant.

Database rule:

- Every tenant-owned table includes `tenant_id`
- `tenant_id` is part of isolation keys, unique constraints, and query indexes
- Shared-platform tables (for example global lookup catalogs, if any) must be explicitly marked as non-tenant and reviewed carefully
- Prefer composite uniqueness scoped by tenant, such as `unique (tenant_id, code)`

---

# 15. Recommended Initial Multi-Tenant Database Strategy

Use:

```text
Shared Database
Shared Schema
tenant_id column on every tenant-owned table
```

Example:

```text
products
--------
id
tenant_id
name_ar
name_en
```

Every tenant-owned table must be keyed for tenant isolation:

- Include `tenant_id` on the table
- Prefer composite unique keys / constraints that include `tenant_id` where business uniqueness is tenant-scoped
- Index query paths as `(tenant_id, ...)`
- Never allow cross-tenant access through shared identifiers alone

Example uniqueness:

```text
unique (tenant_id, barcode)
unique (tenant_id, code)
```

Do not create a separate PostgreSQL database per tenant in the initial version unless there is a real operational or regulatory requirement.

---

# 16. Tenant Isolation

Every tenant-owned entity must include `TenantId` in the domain model, mapped to lowercase column `tenant_id`.

```csharp
public Guid TenantId { get; set; }
```

Database mapping example:

```text
column: tenant_id
```

Every query must automatically filter by the current tenant.

Prefer EF Core Global Query Filters where appropriate.

Example concept:

```csharp
builder.Entity<Product>()
    .HasQueryFilter(x => x.TenantId == currentTenant.Id);
```

EF configuration must map table and column names to lowercase:

```csharp
builder.ToTable("products");
builder.Property(x => x.TenantId).HasColumnName("tenant_id");
```

Never rely only on frontend filtering.

Tenant validation must happen on the backend.

---

# 17. Tenant Resolution

The tenant may later be resolved from:

- Subdomain
- JWT claim
- Logged-in user's tenant
- Custom domain
- Request header for trusted internal communication

Preferred authenticated request approach:

```text
JWT
 |
 +-- user_id
 +-- tenant_id
 +-- roles
 +-- permissions
```

Claims map to domain values such as `UserId` and `TenantId`, while database columns remain lowercase.

---

# 18. Branch and Warehouse Model

Do not hard-code inventory only against pharmacies.

Use a generic stock location concept.

Example:

```text
locations
---------
id
tenant_id
name_ar
name_en
type
```

Location types:

```text
branch
warehouse
```

Possible future types:

```text
distribution_center
virtual_location
returns_location
damaged_stock
```

---

# 19. Inventory Principle

Do not store the entire inventory logic only as:

```text
products.quantity
```

Inventory should primarily be driven by stock movements.

Example:

```text
inventory_transactions
----------------------
id
tenant_id
location_id
product_id
batch_id
transaction_type
quantity
reference_type
reference_id
created_at
```

Possible transaction types:

```text
purchase
sale
sale_return
purchase_return
transfer_in
transfer_out
adjustment
expired
damaged
opening_balance
```

This is essential for traceability and future auditing.

---

# 20. Product Batch / Lot Support

Pharmacy products should support batches.

Example:

```text
product_batches
---------------
id
tenant_id
product_id
batch_number
expiry_date
purchase_price
available_quantity
```

Inventory allocation should support FEFO:

```text
First Expiry First Out
```

---

# 21. Arabic and English

The system must be bilingual.

Supported languages:

```text
Arabic
English
```

Arabic:

```text
ar
```

English:

```text
en
```

---

# 22. RTL / LTR

Angular frontend must support:

```text
English => LTR
Arabic  => RTL
```

Direction should change dynamically based on selected language.

Do not implement a separate Arabic website and English website.

Use a common component system.

---

# 23. Database Translation Fields

For master data managed inside the application, use clear bilingual fields where practical.

Domain / C# properties may remain PascalCase. Database columns must be lowercase:

```text
name_ar
name_en
description_ar
description_en
```

Example entity:

```csharp
public class Category
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
}
```

Mapped columns:

```text
id
tenant_id
name_ar
name_en
```

Avoid storing display text only in English.

---

# 24. Frontend Localization

All fixed UI strings must come from translation resources.

Do not write:

```html
<button>Save</button>
```

Prefer:

```html
<button>{{ 'COMMON.SAVE' | translate }}</button>
```

Example translation keys:

```json
{
  "COMMON": {
    "SAVE": "Save",
    "CANCEL": "Cancel",
    "DELETE": "Delete"
  }
}
```

Arabic:

```json
{
  "COMMON": {
    "SAVE": "حفظ",
    "CANCEL": "إلغاء",
    "DELETE": "حذف"
  }
}
```

---

# 25. Landing Page

The project should include a public landing page separate from the authenticated pharmacy management area.

Example routes:

```text
/
 /features
 /pricing
 /contact
 /login
```

Authenticated application:

```text
/app
```

or:

```text
/dashboard
```

---

# 26. Landing Page Content

Initial landing page sections:

```text
Hero Section
Features
Why Pharmacy
Branches & Warehouses
Inventory Management
Expiry Management
Sales & POS
Reports
Arabic / English Support
Security
Pricing
Contact
Login
```

Primary call to action:

```text
Start Free Trial
```

or:

```text
Request Demo
```

The exact commercial model can be decided later.

---

# 27. Authentication

Use secure authentication.

Recommended:

```text
ASP.NET Core Identity
JWT Access Token
Refresh Token
```

Support later:

```text
2FA
Password reset
Email verification
Phone verification
SSO
```

Passwords must never be stored directly.

---

# 28. Authorization

Use both:

```text
Roles
Permissions
```

Example roles:

```text
Owner
Admin
PharmacyManager
Pharmacist
Cashier
WarehouseManager
Accountant
```

Examples of permissions:

```text
Product.View
Product.Create
Product.Edit

Sale.Create
Sale.Return

Purchase.Create
Purchase.Receive

Inventory.View
Inventory.Adjust

Report.Sales
Report.Profit

Settings.Manage
```

Avoid hard-coding all business authorization only by role names.

---

# 29. API Design

Follow REST conventions where practical.

Examples:

```http
GET    /api/v1/products
GET    /api/v1/products/{id}
POST   /api/v1/products
PUT    /api/v1/products/{id}
DELETE /api/v1/products/{id}
```

Search example:

```http
GET /api/v1/products?search=augmentin&page=1&pageSize=20
```

---

# 30. API Response Standards

Use consistent responses.

Example success response:

```json
{
  "data": {},
  "success": true,
  "errors": []
}
```

Validation response should return structured validation errors.

Avoid exposing internal exception messages to clients.

---

# 31. Pagination

Any endpoint that can return large collections must support pagination.

Example:

```text
page
pageSize
sortBy
sortDirection
search
```

Set a maximum page size.

Example:

```text
Maximum pageSize = 100
```

Do not allow endpoints to return hundreds of thousands of rows because someone clicked "Show All". Human curiosity is not a scalability strategy.

---

# 32. High Performance Requirements

Performance must be considered from the beginning.

Use:

- Async database calls
- CancellationToken
- AsNoTracking
- Projection
- Pagination
- Proper database indexes
- Efficient SQL
- Connection pooling
- Response caching where appropriate
- Distributed caching when justified
- Avoid N+1 queries
- Avoid unnecessary Include chains
- Background processing for expensive operations

---

# 33. Query Projection

Prefer:

```csharp
.Select(x => new ProductDto
{
    Id = x.Id,
    NameAr = x.NameAr,
    NameEn = x.NameEn
})
```

instead of loading complete entities when not required.

---

# 34. PostgreSQL Indexing

Create indexes based on real query patterns.

Important examples:

```text
tenant_id
tenant_id + product_id
tenant_id + barcode
tenant_id + location_id
tenant_id + created_at
tenant_id + expiry_date
tenant_id + supplier_id
```

Example:

```text
ix_products_tenant_id_barcode
```

Avoid creating indexes blindly on every column.

---

# 35. Caching

Caching should be introduced intentionally.

Potential cache candidates:

```text
System settings
Tenant settings
Permissions
Static lookup tables
Categories
Frequently requested product metadata
```

Do not cache highly volatile inventory quantities without a clear invalidation strategy.

Redis may be introduced later if required.

The initial solution should not depend on Redis unless actual load or functional requirements justify it.

---

# 36. Background Jobs

Background operations may include:

```text
Expiry notifications
Low-stock alerts
Scheduled reports
Data aggregation
Invoice exports
Email notifications
SMS notifications
Inventory analytics
```

Possible future technology:

```text
Hangfire
```

or another reliable job processing mechanism.

---

# 37. Audit Logging

Important business operations must be auditable.

Audit record should include:

```text
tenant_id
user_id
action
entity_type
entity_id
old_values
new_values
timestamp
ip_address
```

Examples:

```text
Product price changed
Sale cancelled
Stock adjusted
Purchase modified
User permission changed
Cash shift reopened
```

---

# 38. Soft Delete

Use soft delete for appropriate business master data.

Example:

```text
is_deleted
deleted_at
deleted_by
```

Do not physically delete historical financial or inventory transactions.

---

# 39. Concurrency

Critical inventory and financial operations must handle concurrency.

Examples:

```text
Selling stock
Receiving stock
Transferring inventory
Stock adjustment
Closing cash shift
```

Do not implement business-critical stock changes as:

```text
Read quantity
Modify in memory
Save
```

without appropriate transaction and concurrency handling.

---

# 40. Database Transactions

Use database transactions for operations that must succeed or fail together.

Examples:

```text
Create Sale
    |
    +-- Create invoice
    +-- Create sale items
    +-- Update inventory movement
    +-- Record payment
```

All related operations should commit together.

---

# 41. Logging

Use structured logging.

Recommended library:

```text
Serilog
```

Logs should include context such as:

```text
tenant_id
user_id
request_id
correlation_id
endpoint
duration
status_code
```

Never log:

```text
Passwords
Access tokens
Refresh tokens
Sensitive payment information
```

---

# 42. Error Handling

Implement centralized exception handling middleware.

Return controlled API errors.

Recommended error categories:

```text
ValidationError
NotFound
Unauthorized
Forbidden
Conflict
BusinessRuleViolation
UnexpectedError
```

---

# 43. Validation

Use:

```text
FluentValidation
```

Validate requests in the Application layer.

Example:

```text
CreateProductCommandValidator
```

Validation rules should not be duplicated unnecessarily between controllers and handlers.

---

# 44. Naming Standards

Backend C# code:

```text
PascalCase
```

for classes and public members.

Examples:

```text
CreateProductCommand
ProductService
InventoryTransaction
```

PostgreSQL database naming must use all-lowercase `snake_case` for every table and every column.

Do not use PascalCase, camelCase, or quoted mixed-case identifiers in PostgreSQL.

Examples:

```text
tables:
  products
  product_batches
  inventory_transactions
  sale_items

columns:
  id
  tenant_id
  name_ar
  name_en
  created_at
  is_deleted
```

EF Core must explicitly map entities to these lowercase names.

This convention is mandatory and must be applied consistently across all modules.

---

# 45. Date and Time

Store dates and times consistently.

Use:

```text
UTC
```

for system timestamps.

Convert to the tenant/user timezone only for display.

Examples (domain properties / mapped columns):

```text
CreatedAt  -> created_at
UpdatedAt  -> updated_at
TransactionAt -> transaction_at
```

Expiry dates may be date-only values where time has no business meaning.

---

# 46. Money

Never use floating point types such as:

```csharp
float
double
```

for financial calculations.

Use:

```csharp
decimal
```

Example:

```csharp
public decimal SellingPrice { get; set; }
```

PostgreSQL should use an appropriate:

```text
numeric
```

type.

---

# 47. Security Basics

Apply:

- HTTPS only
- Secure headers
- Authentication
- Authorization
- Tenant isolation
- Rate limiting
- Input validation
- Parameterized SQL / EF Core
- Secure password hashing
- Refresh token rotation
- CORS restrictions
- Audit logs
- Request size limits
- File upload validation

Never trust the Angular application for security enforcement.

All critical rules must be enforced by the backend.

---

# 48. Rate Limiting

Apply backend rate limits especially for:

```text
Login
OTP
Password reset
Registration
Public endpoints
Search abuse
Export endpoints
```

Rate limiting should also be supported at the infrastructure / edge layer.

---

# 49. Angular Structure

Suggested Angular structure:

```text
src/app/
|
|-- core/
|
|-- shared/
|
|-- layout/
|
|-- features/
|   |
|   |-- auth/
|   |-- dashboard/
|   |-- products/
|   |-- inventory/
|   |-- sales/
|   |-- purchases/
|   |-- suppliers/
|   |-- customers/
|   |-- reports/
|   |-- settings/
|
|-- app.routes.ts
```

---

# 50. Angular Core

`core` should contain application-wide services such as:

```text
AuthService
TenantService
LocalizationService
ApiService
ErrorHandler
HTTP Interceptors
Route Guards
```

---

# 51. Angular Shared

`shared` should contain reusable UI building blocks:

```text
Buttons
Tables
Dialogs
Inputs
Date pickers
Dropdowns
Search controls
Loading indicators
Pagination
Validation messages
```

Avoid duplicating the same components across features.

---

# 52. Angular State Management

Do not introduce a heavy state management library automatically.

Use:

```text
Services
Signals
RxJS
```

first.

Introduce NgRx only if application complexity genuinely requires centralized event-based state management.

---

# 53. API Interceptors

Recommended Angular interceptors:

```text
AuthenticationInterceptor
TenantInterceptor if required
ErrorInterceptor
LoadingInterceptor
CorrelationIdInterceptor
```

---

# 54. Environment Configuration

Backend:

```text
appsettings.json
appsettings.Development.json
appsettings.Production.json
```

Do not commit secrets.

Use environment variables or a secure secret store.

Frontend:

```text
environment.ts
environment.development.ts
environment.production.ts
```

---

# 55. Testing

Backend:

```text
Unit Tests
Integration Tests
```

Critical areas requiring integration tests:

```text
Tenant isolation
Inventory updates
Sale creation
Stock transfer
Purchase receiving
Authentication
Authorization
Database transactions
```

Frontend should include tests for critical reusable logic and important workflows.

---

# 56. API Documentation

Use:

```text
Swagger / OpenAPI
```

Development and staging environments should expose clear API documentation.

Document:

```text
Request models
Response models
Authentication
Validation errors
Pagination
```

---

# 57. Health Checks

Provide endpoints for operational monitoring.

Example:

```http
GET /health
```

Checks may include:

```text
API
PostgreSQL
Cache
External services
```

---

# 58. Deployment Independence

Backend and frontend should deploy separately.

Example:

```text
Angular
   |
   +-- Static hosting / CDN

.NET API
   |
   +-- Application hosting

PostgreSQL
   |
   +-- Managed database
```

The exact cloud hosting provider is not mandated by this document.

---

# 59. CI/CD

Recommended pipeline stages:

```text
Restore
Build
Test
Security checks
Publish
Database migration validation
Deploy
Smoke test
```

Frontend pipeline:

```text
npm install
lint
test
build
deploy
```

---

# 60. Initial Business Modules

The initial product should prepare for:

```text
Tenant Management
Users & Permissions
Branches
Warehouses

Products
Categories
Brands

Suppliers
Purchasing

Inventory
Batches
Expiry

POS
Sales
Returns

Customers

Cash Shifts
Expenses

Reports

Settings
Audit Logs
```

---

# 61. Future Modules

Possible future additions:

```text
Smart Reordering
Dead Stock Analysis
Supplier Price Comparison
Lost Sales
Near Expiry Redistribution
Loyalty
Chronic Medication Reminders
Mobile Owner Dashboard
Accounting Integration
E-Commerce Integration
Delivery Integration
Advanced Analytics
AI-assisted insights
```

These should not unnecessarily complicate the MVP.

---

# 62. Development Principles

Always prefer:

```text
Simple
Readable
Maintainable
Testable
Secure
Performant
```

over unnecessarily clever implementations.

---

# 63. Important Project Rules

1. Project name is `Pharmacy`.
2. Backend uses ASP.NET Core.
3. Database is PostgreSQL.
4. ORM is Entity Framework Core.
5. Database schema is maintained using EF Core migrations.
6. Use CQRS.
7. Start as a Modular Monolith.
8. Backend and frontend are separate projects.
9. Frontend uses Angular.
10. Always use Angular Reactive Forms for business forms.
11. System must be multi-tenant from the beginning.
12. Every tenant-owned table must include `tenant_id` and be keyed/indexed for tenant isolation.
13. All PostgreSQL table and column names must be lowercase `snake_case`.
14. System must support Arabic and English.
15. Angular must support both RTL and LTR.
16. All APIs must consider performance and pagination.
17. Use async operations throughout backend I/O.
18. Use PostgreSQL indexes based on real query patterns, typically starting with `tenant_id`.
19. Financial values must use decimal/numeric types.
20. Audit important financial, stock, and security operations.
21. Do not physically delete historical business transactions.
22. Do not trust frontend validation for security or business rules.
23. Landing page is part of the frontend scope.
24. Authenticated application and public landing page must remain logically separated.
25. Avoid microservices until there is a demonstrated need.
26. Avoid unnecessary infrastructure complexity in the MVP.

---

# 64. Initial High-Level Architecture

```text
                    Internet
                       |
                       v
              +----------------+
              | Angular        |
              | Web Frontend   |
              +----------------+
                       |
                       | HTTPS / REST API
                       v
              +----------------+
              | ASP.NET Core   |
              | Pharmacy API   |
              +----------------+
                       |
          +------------+------------+
          |                         |
          v                         v
 +----------------+       +----------------+
 | Application    |       | Infrastructure |
 | CQRS           |       | EF Core        |
 +----------------+       +----------------+
          |                         |
          +------------+------------+
                       |
                       v
              +----------------+
              | PostgreSQL     |
              +----------------+
```

---

# 65. Multi-Tenant Hierarchy

```text
Platform
|
+-- Tenant / Pharmacy Business
    |
    +-- Branch
    |
    +-- Branch
    |
    +-- Warehouse
    |
    +-- Users
    |
    +-- Products
    |
    +-- Suppliers
    |
    +-- Customers
```

---

# 66. Main Technical Objective

The technical foundation should make it possible to launch a simple pharmacy management product first while remaining capable of supporting:

```text
Multiple tenants
Multiple branches
Multiple warehouses
Large product catalogs
High transaction volume
Arabic and English
Advanced inventory workflows
Future SaaS scaling
```

without redesigning the entire system.

The platform should optimize for operational simplicity for pharmacy staff while maintaining strong technical foundations for security, performance, maintainability, and future expansion.
