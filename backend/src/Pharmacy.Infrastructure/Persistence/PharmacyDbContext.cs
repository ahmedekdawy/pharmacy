using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Audit;
using Pharmacy.Domain.CashManagement;
using Pharmacy.Domain.Catalog;
using Pharmacy.Domain.Common;
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
using Pharmacy.Infrastructure.Identity;
using Pharmacy.Infrastructure.Tenancy;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class PharmacyDbContext(
    DbContextOptions<PharmacyDbContext> options,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser) : DbContext(options), IPharmacyDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnItem> SaleReturnItems => Set<SaleReturnItem>();
    public DbSet<CashShift> CashShifts => Set<CashShift>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RolePage> RolePages => Set<RolePage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PharmacyDbContext).Assembly);
        ApplyTenantAndSoftDeleteFilters(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTimeOffset.UtcNow;
        var userId = currentUser.UserId;
        var tenantId = currentTenant.TenantId;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
                entry.Entity.CreatedBy = userId;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
                entry.Entity.UpdatedBy = userId;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added && tenantId.HasValue && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = tenantId.Value;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = utcNow;
                entry.Entity.DeletedBy = userId;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTenantAndSoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType) &&
                typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                var method = typeof(PharmacyDbContext)
                    .GetMethod(nameof(SetTenantSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(clrType);
                method.Invoke(this, [modelBuilder]);
            }
            else if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                var method = typeof(PharmacyDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(clrType);
                method.Invoke(this, [modelBuilder]);
            }
        }
    }

    private void SetTenantSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            !e.IsDeleted &&
            currentTenant.TenantId != null &&
            e.TenantId == currentTenant.TenantId);
    }

    private void SetSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }
}
