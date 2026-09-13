using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Reporting;

public sealed record SalesReportDto(
    DateTimeOffset From,
    DateTimeOffset To,
    int SalesCount,
    decimal SalesTotal,
    int ReturnsCount,
    decimal ReturnsTotal,
    decimal ExpensesTotal,
    decimal NetSales);

public sealed record GetSalesReportQuery(DateTimeOffset? From, DateTimeOffset? To) : IRequest<SalesReportDto>;

public sealed class GetSalesReportQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetSalesReportQuery, SalesReportDto>
{
    public async Task<SalesReportDto> Handle(GetSalesReportQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        var to = request.To ?? DateTimeOffset.UtcNow;
        var from = request.From ?? to.AddDays(-7);

        var sales = await db.Sales.AsNoTracking()
            .Where(x => x.SoldAt >= from && x.SoldAt <= to)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(x => x.TotalAmount) })
            .FirstOrDefaultAsync(cancellationToken);

        var returns = await db.SaleReturns.AsNoTracking()
            .Where(x => x.ReturnedAt >= from && x.ReturnedAt <= to)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(x => x.TotalAmount) })
            .FirstOrDefaultAsync(cancellationToken);

        var expensesTotal = await db.Expenses.AsNoTracking()
            .Where(x => x.SpentAt >= from && x.SpentAt <= to)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;

        var salesCount = sales?.Count ?? 0;
        var salesTotal = sales?.Total ?? 0;
        var returnsCount = returns?.Count ?? 0;
        var returnsTotal = returns?.Total ?? 0;

        return new SalesReportDto(
            from,
            to,
            salesCount,
            salesTotal,
            returnsCount,
            returnsTotal,
            expensesTotal,
            salesTotal - returnsTotal);
    }
}
