using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Inventory.Queries.GetNearExpiry;

public sealed record NearExpiryDto(
    Guid BatchId,
    Guid ProductId,
    string ProductCode,
    string ProductNameEn,
    string BatchNumber,
    DateOnly ExpiryDate,
    decimal AvailableQuantity);

public sealed record GetNearExpiryQuery(int Days = 90) : IRequest<IReadOnlyList<NearExpiryDto>>;

public sealed class GetNearExpiryQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetNearExpiryQuery, IReadOnlyList<NearExpiryDto>>
{
    public async Task<IReadOnlyList<NearExpiryDto>> Handle(GetNearExpiryQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var days = request.Days < 1 ? 90 : request.Days;
        var until = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(days));

        return await (
            from b in db.ProductBatches.AsNoTracking()
            join p in db.Products.AsNoTracking() on b.ProductId equals p.Id
            where b.ExpiryDate != null && b.ExpiryDate <= until && b.AvailableQuantity > 0
            orderby b.ExpiryDate
            select new NearExpiryDto(
                b.Id,
                p.Id,
                p.Code,
                p.NameEn,
                b.BatchNumber,
                b.ExpiryDate!.Value,
                b.AvailableQuantity)
        ).ToListAsync(cancellationToken);
    }
}
