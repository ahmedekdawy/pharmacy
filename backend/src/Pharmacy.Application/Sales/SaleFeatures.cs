using Pharmacy.Shared.Localization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Application.Customers;
using Pharmacy.Domain.Catalog;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales;

/// <param name="Unit">Box (default), Strip or Unit. Quantity is expressed in this unit.</param>
public sealed record SaleLineInput(
    Guid ProductId,
    Guid? BatchId,
    decimal Quantity,
    decimal? UnitPrice,
    SaleUnit Unit = SaleUnit.Box);

/// <param name="Id">Optional client-generated id. Makes the request idempotent (safe to retry / sync).</param>
/// <param name="Number">Optional client-generated invoice number (used by offline devices).</param>
/// <param name="SoldAt">Optional time of sale on the device (offline sales).</param>
/// <param name="PaidAmount">Amount paid now. When null the full total is considered paid.</param>
/// <param name="IsOffline">True when the sale was recorded offline; stock shortages become warnings instead of errors.</param>
public sealed record CreateSaleCommand(
    Guid LocationId,
    Guid? CustomerId,
    string? Notes,
    IReadOnlyList<SaleLineInput> Items,
    Guid? Id = null,
    string? Number = null,
    DateTimeOffset? SoldAt = null,
    decimal DiscountAmount = 0,
    decimal? PaidAmount = null,
    string? PaymentMethod = null,
    string? DeviceId = null,
    bool IsOffline = false) : IRequest<CreateSaleResult>;

public sealed record CreateSaleResult(
    Guid Id,
    string Number,
    decimal TotalAmount,
    decimal DueAmount,
    bool AlreadyExisted,
    IReadOnlyList<string> Warnings);

public sealed record SaleDto(
    Guid Id,
    string Number,
    Guid LocationId,
    string LocationNameEn,
    Guid? CustomerId,
    string? CustomerNameEn,
    string? CustomerNameAr,
    DateTimeOffset SoldAt,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal DueAmount,
    string PaymentMethod,
    string Source,
    bool HasStockWarning,
    string Status);

public sealed record GetSalesQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? CustomerId = null,
    int Take = 100) : IRequest<IReadOnlyList<SaleDto>>;

public sealed class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidAmount).GreaterThanOrEqualTo(0).When(x => x.PaidAmount.HasValue);
        RuleFor(x => x.Number).MaximumLength(64);
        RuleFor(x => x.DeviceId).MaximumLength(64);
        RuleFor(x => x.Notes).MaximumLength(512);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.Unit).IsInEnum();
            item.RuleFor(i => i.UnitPrice).GreaterThan(0).When(i => i.UnitPrice.HasValue)
                .WithMessage("Unit price must be greater than zero.");
        });
    }
}

public sealed class GetSalesQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetSalesQuery, IReadOnlyList<SaleDto>>
{
    public async Task<IReadOnlyList<SaleDto>> Handle(GetSalesQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        var sales = db.Sales.AsNoTracking();
        if (request.From.HasValue) sales = sales.Where(x => x.SoldAt >= request.From);
        if (request.To.HasValue) sales = sales.Where(x => x.SoldAt < request.To);
        if (request.CustomerId.HasValue) sales = sales.Where(x => x.CustomerId == request.CustomerId);

        var take = request.Take is < 1 or > 1000 ? 100 : request.Take;

        return await (
            from s in sales
            join l in db.Locations.AsNoTracking() on s.LocationId equals l.Id
            join c in db.Customers.AsNoTracking() on s.CustomerId equals c.Id into customers
            from c in customers.DefaultIfEmpty()
            orderby s.SoldAt descending
            select new SaleDto(
                s.Id,
                s.Number,
                l.Id,
                l.NameEn,
                c != null ? c.Id : null,
                c != null ? c.NameEn : null,
                c != null ? c.NameAr : null,
                s.SoldAt,
                s.SubTotal,
                s.DiscountAmount,
                s.TotalAmount,
                s.PaidAmount,
                s.DueAmount,
                s.PaymentMethod,
                s.Source,
                s.HasStockWarning,
                s.Status)
        ).Take(take).ToListAsync(cancellationToken);
    }
}

public sealed class CreateSaleCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<CreateSaleCommand, CreateSaleResult>
{
    public async Task<CreateSaleResult> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");
        var tenantId = currentTenant.TenantId.Value;
        var now = DateTimeOffset.UtcNow;
        var warnings = new List<string>();

        // Idempotency: a retried / re-synced sale returns the original result.
        if (request.Id.HasValue)
        {
            var existing = await db.Sales.AsNoTracking()
                .Where(x => x.Id == request.Id.Value)
                .Select(x => new { x.Id, x.Number, x.TotalAmount, x.DueAmount })
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                return new CreateSaleResult(existing.Id, existing.Number, existing.TotalAmount, existing.DueAmount, true, []);
        }

        if (!await db.Locations.AnyAsync(x => x.Id == request.LocationId, cancellationToken))
            throw new InvalidOperationException("Location not found.");

        if (request.CustomerId.HasValue &&
            !await db.Customers.AnyAsync(x => x.Id == request.CustomerId, cancellationToken))
            throw new InvalidOperationException("Customer not found.");

        var soldAt = request.SoldAt ?? now;
        if (soldAt > now.AddMinutes(10)) soldAt = now; // device clock in the future

        var sale = new Sale
        {
            Id = request.Id ?? Guid.NewGuid(),
            TenantId = tenantId,
            Number = await ResolveNumberAsync(request.Number, soldAt, cancellationToken),
            LocationId = request.LocationId,
            CustomerId = request.CustomerId,
            SoldAt = soldAt,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Status = "Completed",
            Source = request.IsOffline ? "Offline" : "Online",
            DeviceId = request.DeviceId,
            PaymentMethod = PaymentMethods.Normalize(request.PaymentMethod),
            CreatedAt = now
        };

        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var batches = await db.ProductBatches
            .Where(x => productIds.Contains(x.ProductId))
            .ToListAsync(cancellationToken);

        decimal subTotal = 0;
        var today = PharmacyClock.Today();

        foreach (var line in request.Items)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                throw new InvalidOperationException("Product not found.");
            if (!product.IsActive && !request.IsOffline)
                throw new InvalidOperationException($"Product {product.Code} is inactive.");
            if (!product.CanSellIn(line.Unit))
            {
                if (!request.IsOffline)
                    throw new InvalidOperationException($"Product {product.Code} cannot be sold by {line.Unit.ToString().ToLowerInvariant()}.");
                warnings.Add($"{product.Code}: sold by {line.Unit} although it is not enabled.");
            }

            var factor = product.FactorFor(line.Unit);
            var unitPrice = line.UnitPrice ?? product.PriceFor(line.Unit);
            var lineTotal = Math.Round(unitPrice * line.Quantity, 2);
            var baseRequired = line.Quantity * factor;
            subTotal += lineTotal;

            var allocations = Allocate(product, line, baseRequired, batches, request.IsOffline, warnings, today);
            if (warnings.Count > 0 && request.IsOffline) sale.HasStockWarning = true;

            // Split the line across batches proportionally, keeping the line total exact.
            decimal allocatedTotal = 0;
            for (var i = 0; i < allocations.Count; i++)
            {
                var (batch, baseQty) = allocations[i];
                var qtyInUnit = baseQty / factor;
                var isLast = i == allocations.Count - 1;
                var sliceTotal = isLast ? lineTotal - allocatedTotal : Math.Round(unitPrice * qtyInUnit, 2);
                allocatedTotal += sliceTotal;

                if (batch is not null) batch.AvailableQuantity -= baseQty;

                sale.Items.Add(new SaleItem
                {
                    TenantId = tenantId,
                    ProductId = product.Id,
                    BatchId = batch?.Id,
                    Quantity = qtyInUnit,
                    Unit = line.Unit,
                    UnitFactor = factor,
                    BaseQuantity = baseQty,
                    UnitPrice = unitPrice,
                    LineTotal = sliceTotal,
                    CreatedAt = now
                });

                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    TenantId = tenantId,
                    LocationId = request.LocationId,
                    ProductId = product.Id,
                    BatchId = batch?.Id,
                    TransactionType = InventoryTransactionType.Sale,
                    Quantity = baseQty,
                    ReferenceType = "Sale",
                    ReferenceId = sale.Id,
                    CreatedAt = now
                });
            }
        }

        // A sale must bring in money. Offline sales already happened on the device, so they are
        // recorded (clamped) with a warning instead of being lost.
        var requestedDiscount = Math.Round(request.DiscountAmount, 2);
        if (requestedDiscount > subTotal)
        {
            if (!request.IsOffline) throw new InvalidOperationException("Discount cannot exceed the invoice subtotal.");
            warnings.Add("Discount cannot exceed the invoice subtotal.");
        }

        var discount = Math.Min(requestedDiscount, subTotal);
        var total = subTotal - discount;
        if (total <= 0 && !request.IsOffline)
            throw new InvalidOperationException("Invoice net total must be greater than zero.");
        var paid = Math.Min(Math.Round(request.PaidAmount ?? total, 2), total);
        var due = total - paid;

        if (due > 0)
        {
            if (sale.CustomerId is null)
                throw new InvalidOperationException("A customer is required for credit (deferred) sales.");

            var customer = await db.Customers.AsNoTracking().FirstAsync(x => x.Id == sale.CustomerId, cancellationToken);
            if (customer.CreditLimit > 0)
            {
                var balance = (await CustomerBalances.GetAsync(db, [customer.Id], cancellationToken))
                    .GetValueOrDefault(customer.Id);
                if (balance + due > customer.CreditLimit)
                {
                    var message = $"Credit limit exceeded for customer {customer.Code} (limit {customer.CreditLimit:0.##}, balance {balance:0.##}).";
                    if (!request.IsOffline) throw new InvalidOperationException(message);
                    warnings.Add(message);
                }
            }
        }

        sale.SubTotal = subTotal;
        sale.DiscountAmount = discount;
        sale.TotalAmount = total;
        sale.PaidAmount = paid;
        sale.DueAmount = due;
        if (warnings.Count > 0)
        {
            var note = string.Join(" | ", warnings);
            sale.Notes = string.IsNullOrEmpty(sale.Notes) ? note : $"{sale.Notes} | {note}";
            if (sale.Notes.Length > 512) sale.Notes = sale.Notes[..512];
        }

        db.Sales.Add(sale);
        await db.SaveChangesAsync(cancellationToken);
        return new CreateSaleResult(sale.Id, sale.Number, sale.TotalAmount, sale.DueAmount, false, ErrorMessages.LocalizeAll(warnings));
    }

    /// <summary>
    /// Picks stock batches FEFO (first expiry, first out), in smallest units.
    /// Online sales fail on shortage; offline sales record the shortage as a warning and
    /// let the last batch go negative so stock reconciles once the purchase is entered.
    /// </summary>
    private static List<(ProductBatch? Batch, decimal BaseQty)> Allocate(
        Product product,
        SaleLineInput line,
        decimal baseRequired,
        List<ProductBatch> batches,
        bool lenient,
        List<string> warnings,
        DateOnly today)
    {
        var result = new List<(ProductBatch?, decimal)>();
        var all = batches.Where(x => x.ProductId == product.Id).ToList();
        // Expired batches (expiry date today or earlier) are never picked for online sales.
        var candidates = all.Where(x => !PharmacyClock.IsExpired(x.ExpiryDate, today)).ToList();
        var expired = all.Where(x => PharmacyClock.IsExpired(x.ExpiryDate, today)).ToList();

        if (line.BatchId.HasValue)
        {
            var chosen = candidates.FirstOrDefault(x => x.Id == line.BatchId);
            if (chosen is null)
            {
                if (!lenient) throw new InvalidOperationException("Batch not found.");
            }
            else
            {
                candidates = [chosen, .. candidates.Where(x => x.Id != chosen.Id)];
            }
        }
        else
        {
            candidates = candidates
                .OrderBy(x => x.ExpiryDate == null)
                .ThenBy(x => x.ExpiryDate)
                .ThenBy(x => x.CreatedAt)
                .ToList();
        }

        var remaining = baseRequired;
        foreach (var batch in candidates)
        {
            if (remaining <= 0) break;
            if (batch.AvailableQuantity <= 0) continue;
            var take = Math.Min(batch.AvailableQuantity, remaining);
            result.Add((batch, take));
            remaining -= take;
        }

        if (remaining > 0 && !lenient)
        {
            throw new InvalidOperationException(expired.Any(x => x.AvailableQuantity > 0)
                ? $"Only expired stock is left for product {product.Code}."
                : $"Insufficient stock for product {product.Code}.");
        }

        // Offline: the goods physically left the shelf, so record them against expired stock if that is all there is.
        foreach (var batch in expired)
        {
            if (remaining <= 0) break;
            if (batch.AvailableQuantity <= 0) continue;
            var take = Math.Min(batch.AvailableQuantity, remaining);
            result.Add((batch, take));
            remaining -= take;
            warnings.Add($"{product.Code}: sold from an expired batch.");
        }
        candidates = [.. candidates, .. expired];

        if (remaining > 0)
        {
            warnings.Add($"{product.Code}: stock short by {remaining:0.##} unit(s).");
            var fallback = candidates.LastOrDefault();
            var existingIndex = result.FindIndex(x => x.Item1 == fallback);
            if (fallback is not null && existingIndex >= 0)
                result[existingIndex] = (fallback, result[existingIndex].Item2 + remaining);
            else
                result.Add((fallback, remaining));
        }

        return result;
    }

    private async Task<string> ResolveNumberAsync(string? requested, DateTimeOffset soldAt, CancellationToken cancellationToken)
    {
        var number = string.IsNullOrWhiteSpace(requested)
            ? $"INV-{soldAt:yyMMdd}-{Random.Shared.Next(0, 1_000_000):000000}"
            : requested.Trim();

        var candidate = number;
        var suffix = 1;
        while (await db.Sales.IgnoreQueryFilters()
                   .AnyAsync(x => x.TenantId == currentTenant.TenantId && x.Number == candidate, cancellationToken))
        {
            candidate = $"{number}-{suffix++}";
        }

        return candidate;
    }
}
