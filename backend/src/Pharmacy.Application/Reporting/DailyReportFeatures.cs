using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Catalog;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Reporting;

public sealed record DailyInvoiceDto(
    Guid Id,
    string Number,
    DateTimeOffset SoldAt,
    string? CustomerNameEn,
    string? CustomerNameAr,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal DueAmount,
    string PaymentMethod,
    string Source,
    bool HasStockWarning);

public sealed record DailyItemDto(
    Guid ProductId,
    string ProductCode,
    string ProductNameEn,
    string ProductNameAr,
    SaleUnit Unit,
    decimal Quantity,
    decimal Total);

public sealed record DailyPaymentDto(
    Guid Id,
    string Number,
    DateTimeOffset PaidAt,
    string CustomerNameEn,
    string CustomerNameAr,
    decimal Amount,
    string PaymentMethod);

public sealed record DailyReportDto(
    DateOnly Date,
    DateTimeOffset From,
    DateTimeOffset To,
    int InvoiceCount,
    int OfflineInvoiceCount,
    int StockWarningCount,
    decimal SubTotal,
    decimal DiscountTotal,
    decimal NetSales,
    decimal CashSales,
    decimal CardSales,
    decimal WalletSales,
    decimal CreditSales,
    decimal DebtCollected,
    decimal DebtCollectedCash,
    decimal ReturnsTotal,
    decimal ReturnsRefunded,
    decimal ReturnsCredited,
    decimal ExpensesTotal,
    decimal CashInDrawer,
    IReadOnlyList<DailyInvoiceDto> Invoices,
    IReadOnlyList<DailyItemDto> Items,
    IReadOnlyList<DailyPaymentDto> Payments);

/// <summary>الفاتورة / اليومية — everything that happened in one business day.</summary>
/// <param name="UtcOffsetMinutes">Client time-zone offset so the day matches the pharmacy's local day (Egypt = 120 / 180).</param>
public sealed record GetDailyReportQuery(DateOnly? Date, Guid? LocationId, int UtcOffsetMinutes = 120) : IRequest<DailyReportDto>;

public sealed class GetDailyReportQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetDailyReportQuery, DailyReportDto>
{
    public async Task<DailyReportDto> Handle(GetDailyReportQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        var offset = TimeSpan.FromMinutes(Math.Clamp(request.UtcOffsetMinutes, -14 * 60, 14 * 60));
        var date = request.Date ?? DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(offset).DateTime);
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();
        var to = from.AddDays(1);

        var salesQuery = db.Sales.AsNoTracking().Where(x => x.SoldAt >= from && x.SoldAt < to);
        var returnsQuery = db.SaleReturns.AsNoTracking().Where(x => x.ReturnedAt >= from && x.ReturnedAt < to);
        var paymentsQuery = db.CustomerPayments.AsNoTracking().Where(x => x.PaidAt >= from && x.PaidAt < to);
        var expensesQuery = db.Expenses.AsNoTracking().Where(x => x.SpentAt >= from && x.SpentAt < to);

        if (request.LocationId.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.LocationId == request.LocationId);
            returnsQuery = returnsQuery.Where(x => x.LocationId == request.LocationId);
            paymentsQuery = paymentsQuery.Where(x => x.LocationId == null || x.LocationId == request.LocationId);
            expensesQuery = expensesQuery.Where(x => x.LocationId == request.LocationId);
        }

        var invoices = await (
            from s in salesQuery
            join c in db.Customers.AsNoTracking() on s.CustomerId equals c.Id into customers
            from c in customers.DefaultIfEmpty()
            orderby s.SoldAt
            select new DailyInvoiceDto(
                s.Id, s.Number, s.SoldAt,
                c != null ? c.NameEn : null, c != null ? c.NameAr : null,
                s.SubTotal, s.DiscountAmount, s.TotalAmount, s.PaidAmount, s.DueAmount,
                s.PaymentMethod, s.Source, s.HasStockWarning)
        ).ToListAsync(cancellationToken);

        var itemRows = await (
            from i in db.SaleItems.AsNoTracking()
            join s in salesQuery on i.SaleId equals s.Id
            join p in db.Products.AsNoTracking() on i.ProductId equals p.Id
            group i by new { p.Id, p.Code, p.NameEn, p.NameAr, i.Unit } into g
            select new DailyItemDto(g.Key.Id, g.Key.Code, g.Key.NameEn, g.Key.NameAr, g.Key.Unit,
                g.Sum(x => x.Quantity), g.Sum(x => x.LineTotal))
        ).ToListAsync(cancellationToken);

        var payments = await (
            from p in paymentsQuery
            join c in db.Customers.AsNoTracking() on p.CustomerId equals c.Id
            orderby p.PaidAt
            select new DailyPaymentDto(p.Id, p.Number, p.PaidAt, c.NameEn, c.NameAr, p.Amount, p.PaymentMethod)
        ).ToListAsync(cancellationToken);

        var returns = await returnsQuery
            .Select(x => new { x.TotalAmount, x.RefundAmount, x.CreditAmount })
            .ToListAsync(cancellationToken);

        var expensesTotal = await expensesQuery.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;

        decimal PaidBy(string method) => invoices.Where(x => x.PaymentMethod == method).Sum(x => x.PaidAmount);

        var cashSales = PaidBy(PaymentMethods.Cash);
        var debtCollectedCash = payments.Where(x => x.PaymentMethod == PaymentMethods.Cash).Sum(x => x.Amount);
        var refunded = returns.Sum(x => x.RefundAmount);

        return new DailyReportDto(
            date,
            from,
            to,
            invoices.Count,
            invoices.Count(x => x.Source == "Offline"),
            invoices.Count(x => x.HasStockWarning),
            invoices.Sum(x => x.SubTotal),
            invoices.Sum(x => x.DiscountAmount),
            invoices.Sum(x => x.TotalAmount),
            cashSales,
            PaidBy(PaymentMethods.Card),
            PaidBy(PaymentMethods.Wallet),
            invoices.Sum(x => x.DueAmount),
            payments.Sum(x => x.Amount),
            debtCollectedCash,
            returns.Sum(x => x.TotalAmount),
            refunded,
            returns.Sum(x => x.CreditAmount),
            expensesTotal,
            cashSales + debtCollectedCash - refunded - expensesTotal,
            invoices,
            itemRows.OrderByDescending(x => x.Total).ToList(),
            payments);
    }
}
