using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Reporting;

public sealed record ProfitReportDto(
    DateTimeOffset From,
    DateTimeOffset To,
    decimal SalesTotal,
    decimal ReturnsTotal,
    decimal CostOfGoodsSold,
    decimal ExpensesTotal,
    decimal GrossProfit,
    decimal NetProfit);

public sealed record GetProfitReportQuery(DateTimeOffset? From, DateTimeOffset? To) : IRequest<ProfitReportDto>;

public sealed class GetProfitReportQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetProfitReportQuery, ProfitReportDto>
{
    public async Task<ProfitReportDto> Handle(GetProfitReportQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        var toUtc = request.To ?? DateTimeOffset.UtcNow;
        var fromUtc = request.From ?? toUtc.AddDays(-7);

        var salesTotal = await db.Sales.AsNoTracking()
            .Where(x => x.SoldAt >= fromUtc && x.SoldAt <= toUtc)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;

        var returnsTotal = await db.SaleReturns.AsNoTracking()
            .Where(x => x.ReturnedAt >= fromUtc && x.ReturnedAt <= toUtc)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;

        var expensesTotal = await db.Expenses.AsNoTracking()
            .Where(x => x.SpentAt >= fromUtc && x.SpentAt <= toUtc)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;

        var cogs = await (
            from si in db.SaleItems.AsNoTracking()
            join s in db.Sales.AsNoTracking() on si.SaleId equals s.Id
            join b in db.ProductBatches.AsNoTracking() on si.BatchId equals b.Id
            where s.SoldAt >= fromUtc && s.SoldAt <= toUtc
            select si.Quantity * b.PurchasePrice
        ).SumAsync(cancellationToken);

        var returnCost = await (
            from ri in db.SaleReturnItems.AsNoTracking()
            join r in db.SaleReturns.AsNoTracking() on ri.SaleReturnId equals r.Id
            join b in db.ProductBatches.AsNoTracking() on ri.BatchId equals b.Id
            where r.ReturnedAt >= fromUtc && r.ReturnedAt <= toUtc
            select ri.Quantity * b.PurchasePrice
        ).SumAsync(cancellationToken);

        var netCogs = cogs - returnCost;
        var gross = salesTotal - returnsTotal - netCogs;
        var net = gross - expensesTotal;

        return new ProfitReportDto(fromUtc, toUtc, salesTotal, returnsTotal, netCogs, expensesTotal, gross, net);
    }
}

public sealed record DashboardDto(
    int ProductCount,
    int LowStockCount,
    int NearExpiryCount,
    decimal TodaySalesTotal,
    int TodaySalesCount,
    int OpenCashShifts,
    decimal MonthExpensesTotal);

public sealed record GetDashboardQuery : IRequest<DashboardDto>;

public sealed class GetDashboardQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        var todayStart = DateTimeOffset.UtcNow.Date;
        var monthStart = new DateTimeOffset(todayStart.Year, todayStart.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var expiryLimit = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90));

        var productCount = await db.Products.AsNoTracking().CountAsync(x => x.IsActive, cancellationToken);

        var lowStockCount = await (
            from b in db.ProductBatches.AsNoTracking()
            join p in db.Products.AsNoTracking() on b.ProductId equals p.Id
            where p.IsActive
            group b by p.Id into g
            where g.Sum(x => x.AvailableQuantity) <= 10
            select g.Key
        ).CountAsync(cancellationToken);

        var nearExpiryCount = await db.ProductBatches.AsNoTracking()
            .CountAsync(x => x.AvailableQuantity > 0 && x.ExpiryDate != null && x.ExpiryDate <= expiryLimit, cancellationToken);

        var todaySales = await db.Sales.AsNoTracking()
            .Where(x => x.SoldAt >= todayStart)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(x => x.TotalAmount) })
            .FirstOrDefaultAsync(cancellationToken);

        var openShifts = await db.CashShifts.AsNoTracking().CountAsync(x => x.Status == "Open", cancellationToken);
        var monthExpenses = await db.Expenses.AsNoTracking()
            .Where(x => x.SpentAt >= monthStart)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;

        return new DashboardDto(
            productCount,
            lowStockCount,
            nearExpiryCount,
            todaySales?.Total ?? 0,
            todaySales?.Count ?? 0,
            openShifts,
            monthExpenses);
    }
}
