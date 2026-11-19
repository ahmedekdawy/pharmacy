using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Identity;
using Pharmacy.Domain.Locations;
using Pharmacy.Domain.Tenants;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Persistence;

public static class IdentityDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentityDataSeeder");

        await db.Database.MigrateAsync(cancellationToken);

        await SeedPermissionsAsync(db, cancellationToken);
        await SeedPagesAsync(db, cancellationToken);
        await SeedDemoTenantAsync(db, passwords, logger, cancellationToken);
        await GrantAllToOwnerRolesAsync(db, cancellationToken);
    }

    /// <summary>Owner roles of every tenant always get newly introduced permissions and pages (e.g. POS, debts).</summary>
    private static async Task GrantAllToOwnerRolesAsync(PharmacyDbContext db, CancellationToken cancellationToken)
    {
        var owners = await db.Roles.IgnoreQueryFilters()
            .Where(x => x.Code == "Owner" && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        if (owners.Count == 0) return;

        var permissions = await db.Permissions.IgnoreQueryFilters().Where(x => x.IsActive).Select(x => x.Id).ToListAsync(cancellationToken);
        var pages = await db.Pages.IgnoreQueryFilters().Where(x => x.IsActive).Select(x => x.Id).ToListAsync(cancellationToken);
        var ownerIds = owners.Select(x => x.Id).ToList();

        var existingPermissions = (await db.RolePermissions.IgnoreQueryFilters()
                .Where(x => ownerIds.Contains(x.RoleId))
                .Select(x => new { x.RoleId, x.PermissionId })
                .ToListAsync(cancellationToken))
            .Select(x => (x.RoleId, x.PermissionId))
            .ToHashSet();
        var existingPages = (await db.RolePages.IgnoreQueryFilters()
                .Where(x => ownerIds.Contains(x.RoleId))
                .Select(x => new { x.RoleId, x.PageId })
                .ToListAsync(cancellationToken))
            .Select(x => (x.RoleId, x.PageId))
            .ToHashSet();

        foreach (var role in owners)
        {
            foreach (var permissionId in permissions.Where(p => !existingPermissions.Contains((role.Id, p))))
            {
                db.RolePermissions.Add(new RolePermission
                {
                    TenantId = role.TenantId,
                    RoleId = role.Id,
                    PermissionId = permissionId,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            foreach (var pageId in pages.Where(p => !existingPages.Contains((role.Id, p))))
            {
                db.RolePages.Add(new RolePage
                {
                    TenantId = role.TenantId,
                    RoleId = role.Id,
                    PageId = pageId,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPermissionsAsync(PharmacyDbContext db, CancellationToken cancellationToken)
    {
        var definitions = new (string Code, string Module, string NameEn, string NameAr)[]
        {
            (PermissionCodes.UserView, "Users", "View users", "عرض المستخدمين"),
            (PermissionCodes.UserCreate, "Users", "Create users", "إنشاء المستخدمين"),
            (PermissionCodes.UserEdit, "Users", "Edit users", "تعديل المستخدمين"),
            (PermissionCodes.UserActivate, "Users", "Activate/deactivate users", "تفعيل/إيقاف المستخدمين"),
            (PermissionCodes.RoleView, "Roles", "View roles", "عرض الأدوار"),
            (PermissionCodes.RoleManage, "Roles", "Manage roles", "إدارة الأدوار"),
            (PermissionCodes.PageView, "Pages", "View pages", "عرض الصفحات"),
            (PermissionCodes.PermissionView, "Permissions", "View permissions", "عرض الصلاحيات"),
            (PermissionCodes.ProductView, "Products", "View products", "عرض المنتجات"),
            (PermissionCodes.ProductCreate, "Products", "Create products", "إنشاء المنتجات"),
            (PermissionCodes.ProductEdit, "Products", "Edit products", "تعديل المنتجات"),
            (PermissionCodes.ProductDelete, "Products", "Delete products", "حذف المنتجات"),
            (PermissionCodes.LocationView, "Locations", "View locations", "عرض المواقع"),
            (PermissionCodes.LocationManage, "Locations", "Manage locations", "إدارة المواقع"),
            (PermissionCodes.CategoryView, "Categories", "View categories", "عرض التصنيفات"),
            (PermissionCodes.CategoryManage, "Categories", "Manage categories", "إدارة التصنيفات"),
            (PermissionCodes.BrandView, "Brands", "View brands", "عرض العلامات"),
            (PermissionCodes.BrandManage, "Brands", "Manage brands", "إدارة العلامات"),
            (PermissionCodes.InventoryView, "Inventory", "View inventory", "عرض المخزون"),
            (PermissionCodes.InventoryAdjust, "Inventory", "Adjust inventory", "تعديل المخزون"),
            (PermissionCodes.SupplierView, "Suppliers", "View suppliers", "عرض الموردين"),
            (PermissionCodes.SupplierManage, "Suppliers", "Manage suppliers", "إدارة الموردين"),
            (PermissionCodes.PurchaseView, "Purchasing", "View purchases", "عرض المشتريات"),
            (PermissionCodes.PurchaseCreate, "Purchasing", "Create purchases", "إنشاء المشتريات"),
            (PermissionCodes.PurchaseReceive, "Purchasing", "Receive purchases", "استلام المشتريات"),
            (PermissionCodes.CustomerView, "Customers", "View customers", "عرض العملاء"),
            (PermissionCodes.CustomerManage, "Customers", "Manage customers", "إدارة العملاء"),
            (PermissionCodes.CustomerPayment, "Customers", "Collect customer payments", "تحصيل مديونيات العملاء"),
            (PermissionCodes.SaleView, "Sales", "View sales", "عرض المبيعات"),
            (PermissionCodes.SaleCreate, "Sales", "Create sales", "إنشاء المبيعات"),
            (PermissionCodes.SaleReturn, "Sales", "Return sales", "مرتجع المبيعات"),
            (PermissionCodes.CashShiftView, "Cash", "View cash shifts", "عرض الورديات"),
            (PermissionCodes.CashShiftOpen, "Cash", "Open cash shift", "فتح الوردية"),
            (PermissionCodes.CashShiftClose, "Cash", "Close cash shift", "إغلاق الوردية"),
            (PermissionCodes.ExpenseView, "Expenses", "View expenses", "عرض المصروفات"),
            (PermissionCodes.ExpenseManage, "Expenses", "Manage expenses", "إدارة المصروفات"),
            (PermissionCodes.ShortageView, "Shortages", "View shortages & report", "عرض النواقص وتقريرها"),
            (PermissionCodes.ShortageCreate, "Shortages", "Add shortages (also from POS)", "إضافة نواقص (ومن نقطة البيع)"),
            (PermissionCodes.ShortageManage, "Shortages", "Manage shortages (status, delete)", "إدارة النواقص (الحالة والحذف)"),
            (PermissionCodes.ReportSales, "Reports", "Sales reports", "تقارير المبيعات"),
            (PermissionCodes.ReportProfit, "Reports", "Profit reports", "تقارير الأرباح"),
            (PermissionCodes.AuditView, "Audit", "View audit logs", "عرض سجل التدقيق"),
            (PermissionCodes.SettingsManage, "Settings", "Manage settings", "إدارة الإعدادات")
        };

        var existing = await db.Permissions.IgnoreQueryFilters().Select(x => x.Code).ToListAsync(cancellationToken);
        foreach (var item in definitions.Where(x => !existing.Contains(x.Code)))
        {
            db.Permissions.Add(new Permission
            {
                Code = item.Code,
                Module = item.Module,
                NameEn = item.NameEn,
                NameAr = item.NameAr,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPagesAsync(PharmacyDbContext db, CancellationToken cancellationToken)
    {
        var definitions = new (string Code, string Route, string NameEn, string NameAr, int Sort)[]
        {
            ("dashboard", "/app/dashboard", "Dashboard", "لوحة التحكم", 1),
            ("products", "/app/products", "Products", "المنتجات", 2),
            ("categories", "/app/categories", "Categories", "التصنيفات", 3),
            ("brands", "/app/brands", "Brands", "العلامات", 4),
            ("locations", "/app/locations", "Locations", "المواقع", 5),
            ("inventory", "/app/inventory", "Inventory", "المخزون", 6),
            ("suppliers", "/app/suppliers", "Suppliers", "الموردون", 7),
            ("purchases", "/app/purchases", "Purchases", "المشتريات", 8),
            ("customers", "/app/customers", "Customers", "العملاء", 9),
            ("sales", "/app/sales", "Sales", "المبيعات", 10),
            ("returns", "/app/returns", "Returns", "المرتجعات", 11),
            ("cash-shifts", "/app/cash-shifts", "Cash Shifts", "الورديات", 12),
            ("expenses", "/app/expenses", "Expenses", "المصروفات", 13),
            ("reports", "/app/reports", "Reports", "التقارير", 14),
            ("audit", "/app/audit", "Audit Logs", "سجل التدقيق", 15),
            ("settings", "/app/settings", "Settings", "الإعدادات", 16),
            ("users", "/app/users", "Users", "المستخدمون", 17),
            ("roles", "/app/roles", "Roles", "الأدوار", 18),
            ("pos", "/app/pos", "Point of Sale", "نقطة البيع", 0),
            ("daily-report", "/app/daily-report", "Daily Invoice", "اليومية", 15),
            ("debts", "/app/debts", "Customer Debts", "المديونيات", 10),
            ("opening-stock", "/app/opening-stock", "Opening Stock Count", "رصيد أول المدة", 6),
            ("shortages", "/app/shortages", "Shortages", "النواقص", 7)
        };

        var existing = await db.Pages.IgnoreQueryFilters().Select(x => x.Code).ToListAsync(cancellationToken);
        foreach (var item in definitions.Where(x => !existing.Contains(x.Code)))
        {
            db.Pages.Add(new Page
            {
                Code = item.Code,
                Route = item.Route,
                NameEn = item.NameEn,
                NameAr = item.NameAr,
                SortOrder = item.Sort,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoTenantAsync(
        PharmacyDbContext db,
        IPasswordService passwords,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Code == "DEMO", cancellationToken);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Code = "DEMO",
                NameEn = "Demo Pharmacy",
                NameAr = "صيدلية تجريبية",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(cancellationToken);
        }

        var role = await db.Roles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Code == "Owner", cancellationToken);

        if (role is null)
        {
            role = new Role
            {
                TenantId = tenant.Id,
                Code = "Owner",
                NameEn = "Owner",
                NameAr = "المالك",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync(cancellationToken);
        }

        var adminEmail = "admin@demo.pharmacy";
        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Email == adminEmail, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                TenantId = tenant.Id,
                Email = adminEmail,
                PasswordHash = passwords.Hash("Admin@123"),
                FullNameEn = "Demo Admin",
                FullNameAr = "مدير تجريبي",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded DEMO tenant admin {Email} / Admin@123", adminEmail);
        }

        var hasUserRole = await db.UserRoles.IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenant.Id && x.UserId == user.Id && x.RoleId == role.Id, cancellationToken);

        if (!hasUserRole)
        {
            db.UserRoles.Add(new UserRole
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                RoleId = role.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var permissions = await db.Permissions.IgnoreQueryFilters().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var existingPermissionIds = await db.RolePermissions.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenant.Id && x.RoleId == role.Id)
            .Select(x => x.PermissionId)
            .ToListAsync(cancellationToken);

        foreach (var permission in permissions.Where(p => !existingPermissionIds.Contains(p.Id)))
        {
            db.RolePermissions.Add(new RolePermission
            {
                TenantId = tenant.Id,
                RoleId = role.Id,
                PermissionId = permission.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var pages = await db.Pages.IgnoreQueryFilters().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var existingPageIds = await db.RolePages.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenant.Id && x.RoleId == role.Id)
            .Select(x => x.PageId)
            .ToListAsync(cancellationToken);

        foreach (var page in pages.Where(p => !existingPageIds.Contains(p.Id)))
        {
            db.RolePages.Add(new RolePage
            {
                TenantId = tenant.Id,
                RoleId = role.Id,
                PageId = page.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var hasBranch = await db.Locations.IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenant.Id && x.Type == LocationType.Branch, cancellationToken);

        if (!hasBranch)
        {
            db.Locations.Add(new Location
            {
                TenantId = tenant.Id,
                NameEn = "Main Branch",
                NameAr = "الفرع الرئيسي",
                Type = LocationType.Branch,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
