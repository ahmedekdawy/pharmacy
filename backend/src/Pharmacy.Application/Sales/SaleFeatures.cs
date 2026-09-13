using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales;

public sealed record SaleLineInput(Guid ProductId, Guid? BatchId, decimal Quantity, decimal? UnitPrice);

public sealed record CreateSaleCommand(
    Guid LocationId,
    Guid? CustomerId,
    string? Notes,
    IReadOnlyList<SaleLineInput> Items) : IRequest<Guid>;

public sealed record SaleDto(
    Guid Id,
    string Number,
    Guid LocationId,
    string LocationNameEn,
    Guid? CustomerId,
    string? CustomerNameEn,
    DateTimeOffset SoldAt,
    decimal TotalAmount,
    string Status);

public sealed record GetSalesQuery : IRequest<IReadOnlyList<SaleDto>>;

public sealed class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}

public sealed class GetSalesQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetSalesQuery, IReadOnlyList<SaleDto>>
{
    public async Task<IReadOnlyList<SaleDto>> Handle(GetSalesQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        return await (
            from s in db.Sales.AsNoTracking()
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
                s.SoldAt,
                s.TotalAmount,
                s.Status)
        ).Take(100).ToListAsync(cancellationToken);
    }
}

public sealed class CreateSaleCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<CreateSaleCommand, Guid>
{
    public async Task<Guid> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");
        var tenantId = currentTenant.TenantId.Value;

        if (!await db.Locations.AnyAsync(x => x.Id == request.LocationId && x.IsActive, cancellationToken))
            throw new InvalidOperationException("Location not found.");

        if (request.CustomerId.HasValue &&
            !await db.Customers.AnyAsync(x => x.Id == request.CustomerId && x.IsActive, cancellationToken))
            throw new InvalidOperationException("Customer not found.");

        var sale = new Sale
        {
            TenantId = tenantId,
            Number = $"SO-{DateTime.UtcNow:yyyyMMddHHmmss}",
            LocationId = request.LocationId,
            CustomerId = request.CustomerId,
            SoldAt = DateTimeOffset.UtcNow,
            Notes = request.Notes,
            Status = "Completed",
            CreatedAt = DateTimeOffset.UtcNow
        };

        decimal total = 0;

        foreach (var line in request.Items)
        {
            var product = await db.Products.FirstOrDefaultAsync(x => x.Id == line.ProductId && x.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("Product not found.");

            ProductBatch? batch;
            if (line.BatchId.HasValue)
            {
                batch = await db.ProductBatches.FirstOrDefaultAsync(x => x.Id == line.BatchId && x.ProductId == line.ProductId, cancellationToken)
                    ?? throw new InvalidOperationException("Batch not found.");
            }
            else
            {
                // FEFO: earliest expiry first
                batch = await db.ProductBatches
                    .Where(x => x.ProductId == line.ProductId && x.AvailableQuantity > 0)
                    .OrderBy(x => x.ExpiryDate == null)
                    .ThenBy(x => x.ExpiryDate)
                    .ThenBy(x => x.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"No stock available for product {product.Code}.");
            }

            if (batch.AvailableQuantity < line.Quantity)
                throw new InvalidOperationException($"Insufficient stock for product {product.Code}.");

            batch.AvailableQuantity -= line.Quantity;

            var unitPrice = line.UnitPrice ?? product.SellingPrice;
            var lineTotal = unitPrice * line.Quantity;
            total += lineTotal;

            sale.Items.Add(new SaleItem
            {
                TenantId = tenantId,
                ProductId = line.ProductId,
                BatchId = batch.Id,
                Quantity = line.Quantity,
                UnitPrice = unitPrice,
                LineTotal = lineTotal,
                CreatedAt = DateTimeOffset.UtcNow
            });

            db.InventoryTransactions.Add(new InventoryTransaction
            {
                TenantId = tenantId,
                LocationId = request.LocationId,
                ProductId = line.ProductId,
                BatchId = batch.Id,
                TransactionType = InventoryTransactionType.Sale,
                Quantity = line.Quantity,
                ReferenceType = "Sale",
                ReferenceId = sale.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        sale.TotalAmount = total;
        db.Sales.Add(sale);
        await db.SaveChangesAsync(cancellationToken);
        return sale.Id;
    }
}
