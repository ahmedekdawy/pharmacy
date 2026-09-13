using Pharmacy.Domain.Audit;
using Pharmacy.Domain.CashManagement;
using Pharmacy.Domain.Catalog;
using Pharmacy.Domain.Customers;
using Pharmacy.Domain.Expenses;
using Pharmacy.Domain.Identity;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Locations;
using Pharmacy.Domain.Purchasing;
using Pharmacy.Domain.Sales;
using Pharmacy.Domain.Settings;
using Pharmacy.Domain.Suppliers;
using Pharmacy.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Pharmacy.Application.Common.Interfaces;

public interface IPharmacyDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Location> Locations { get; }
    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductBatch> ProductBatches { get; }
    DbSet<InventoryTransaction> InventoryTransactions { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Purchase> Purchases { get; }
    DbSet<PurchaseItem> PurchaseItems { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }
    DbSet<SaleReturn> SaleReturns { get; }
    DbSet<SaleReturnItem> SaleReturnItems { get; }
    DbSet<CashShift> CashShifts { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<TenantSetting> TenantSettings { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<Page> Pages { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RolePage> RolePages { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
