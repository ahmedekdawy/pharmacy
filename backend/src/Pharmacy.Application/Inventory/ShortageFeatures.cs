using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Inventory;

namespace Pharmacy.Application.Inventory;

// ---------------------------------------------------------------- contracts

/// <param name="Id">Optional client-generated id (makes offline / retried requests idempotent).</param>
/// <param name="ProductId">Catalog product. When null, <paramref name="ItemName"/> is required (item not stocked yet).</param>
/// <param name="Quantity">Requested quantity in boxes.</param>
/// <param name="Source">"POS" when added while selling, otherwise "Manual".</param>
public sealed record CreateShortageCommand(
    Guid? ProductId,
    string? ItemName,
    decimal Quantity = 1,
    Guid? LocationId = null,
    string? Notes = null,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? Source = null,
    Guid? Id = null) : IRequest<CreateShortageResult>;

/// <param name="Merged">True when the request was added to an existing open shortage for the same item.</param>
public sealed record CreateShortageResult(Guid Id, bool Merged, int RequestCount, decimal Quantity);

public sealed record UpdateShortageCommand(
    Guid Id,
    decimal Quantity,
    string Status,
    string? Notes,
    string? CustomerName,
    string? CustomerPhone) : IRequest;

public sealed record DeleteShortageCommand(Guid Id) : IRequest;

/// <param name="Status">Open, Ordered, Received, Cancelled, or "Active" (open + ordered). Null = all.</param>
/// <param name="From">Shortages with any request on/after this time.</param>
/// <param name="To">Shortages first requested on/before this time.</param>
public sealed record GetShortagesQuery(
    string? Status = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? LocationId = null,
    string? Search = null) : IRequest<IReadOnlyList<ShortageDto>>;

public sealed record ShortageDto(
    Guid Id,
    Guid? ProductId,
    string? ProductCode,
    string ItemName,
    string? ProductNameEn,
    string? ProductNameAr,
    Guid? LocationId,
    string? LocationNameEn,
    string? LocationNameAr,
    decimal Quantity,
    int RequestCount,
    string Status,
    string? Notes,
    string? CustomerName,
    string? CustomerPhone,
    string Source,
    DateTimeOffset RequestedAt,
    DateTimeOffset LastRequestedAt,
    DateTimeOffset? ResolvedAt,
    string? RequestedByEn,
    string? RequestedByAr,
    decimal? StockBoxes);

// ---------------------------------------------------------------- validation

public sealed class CreateShortageCommandValidator : AbstractValidator<CreateShortageCommand>
{
    public CreateShortageCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.ProductId.HasValue || !string.IsNullOrWhiteSpace(x.ItemName))
            .WithMessage("Enter the item name or choose a product.");
        RuleFor(x => x.ItemName).MaximumLength(256);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(100_000);
        RuleFor(x => x.Notes).MaximumLength(512);
        RuleFor(x => x.CustomerName).MaximumLength(128);
        RuleFor(x => x.CustomerPhone).MaximumLength(32);
    }
}

public sealed class UpdateShortageCommandValidator : AbstractValidator<UpdateShortageCommand>
{
    public UpdateShortageCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(100_000);
        RuleFor(x => x.Status).Must(s => ShortageStatuses.Normalize(s) is not null).WithMessage("Invalid shortage status.");
        RuleFor(x => x.Notes).MaximumLength(512);
        RuleFor(x => x.CustomerName).MaximumLength(128);
        RuleFor(x => x.CustomerPhone).MaximumLength(32);
    }
}

// ---------------------------------------------------------------- handlers

public sealed class CreateShortageCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<CreateShortageCommand, CreateShortageResult>
{
    public async Task<CreateShortageResult> Handle(CreateShortageCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");
        var now = DateTimeOffset.UtcNow;

        // Idempotency for offline / retried requests.
        if (request.Id.HasValue)
        {
            var same = await db.Shortages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (same is not null) return new CreateShortageResult(same.Id, false, same.RequestCount, same.Quantity);
        }

        string itemName;
        if (request.ProductId.HasValue)
        {
            var product = await db.Products.AsNoTracking()
                .Where(x => x.Id == request.ProductId)
                .Select(x => new { x.NameAr, x.NameEn })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Product not found.");
            itemName = string.IsNullOrWhiteSpace(request.ItemName) ? product.NameEn : request.ItemName.Trim();
        }
        else
        {
            itemName = request.ItemName!.Trim();
        }

        if (request.LocationId.HasValue && !await db.Locations.AnyAsync(x => x.Id == request.LocationId, cancellationToken))
            throw new InvalidOperationException("Location not found.");

        // Same item still open → count the request instead of adding a duplicate line.
        var open = db.Shortages.Where(x =>
            (x.Status == ShortageStatuses.Open || x.Status == ShortageStatuses.Ordered) &&
            x.LocationId == request.LocationId);
        var lowered = itemName.ToLower();
        var existing = request.ProductId.HasValue
            ? await open.FirstOrDefaultAsync(x => x.ProductId == request.ProductId, cancellationToken)
            : await open.FirstOrDefaultAsync(x => x.ProductId == null && x.ItemName.ToLower() == lowered, cancellationToken);

        if (existing is not null)
        {
            existing.Quantity += request.Quantity;
            existing.RequestCount += 1;
            existing.LastRequestedAt = now;
            existing.Notes = Append(existing.Notes, request.Notes, 512);
            existing.CustomerName ??= Clean(request.CustomerName);
            existing.CustomerPhone ??= Clean(request.CustomerPhone);
            await db.SaveChangesAsync(cancellationToken);
            return new CreateShortageResult(existing.Id, true, existing.RequestCount, existing.Quantity);
        }

        var shortage = new Shortage
        {
            Id = request.Id ?? Guid.NewGuid(),
            TenantId = currentTenant.TenantId.Value,
            ProductId = request.ProductId,
            ItemName = itemName,
            LocationId = request.LocationId,
            Quantity = request.Quantity,
            RequestCount = 1,
            Status = ShortageStatuses.Open,
            Notes = Clean(request.Notes),
            CustomerName = Clean(request.CustomerName),
            CustomerPhone = Clean(request.CustomerPhone),
            Source = string.Equals(request.Source, "POS", StringComparison.OrdinalIgnoreCase) ? "POS" : "Manual",
            RequestedAt = now,
            LastRequestedAt = now,
            CreatedAt = now
        };
        db.Shortages.Add(shortage);
        await db.SaveChangesAsync(cancellationToken);
        return new CreateShortageResult(shortage.Id, false, 1, shortage.Quantity);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Append(string? current, string? addition, int max)
    {
        var add = Clean(addition);
        if (add is null) return current;
        if (string.IsNullOrEmpty(current)) return add.Length > max ? add[..max] : add;
        if (current.Contains(add, StringComparison.OrdinalIgnoreCase)) return current;
        var joined = $"{current} | {add}";
        return joined.Length > max ? joined[..max] : joined;
    }
}

public sealed class UpdateShortageCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<UpdateShortageCommand>
{
    public async Task Handle(UpdateShortageCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var entity = await db.Shortages.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Shortage not found.");

        var status = ShortageStatuses.Normalize(request.Status)!;
        if (status != entity.Status)
        {
            entity.ResolvedAt = ShortageStatuses.IsActive(status) ? null : DateTimeOffset.UtcNow;
            entity.Status = status;
        }

        entity.Quantity = request.Quantity;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entity.CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName.Trim();
        entity.CustomerPhone = string.IsNullOrWhiteSpace(request.CustomerPhone) ? null : request.CustomerPhone.Trim();
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteShortageCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<DeleteShortageCommand>
{
    public async Task Handle(DeleteShortageCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var entity = await db.Shortages.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Shortage not found.");
        db.Shortages.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class GetShortagesQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetShortagesQuery, IReadOnlyList<ShortageDto>>
{
    public async Task<IReadOnlyList<ShortageDto>> Handle(GetShortagesQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        var query = db.Shortages.AsNoTracking();

        if (string.Equals(request.Status, "Active", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Status == ShortageStatuses.Open || x.Status == ShortageStatuses.Ordered);
        else if (ShortageStatuses.Normalize(request.Status) is { } status)
            query = query.Where(x => x.Status == status);

        if (request.From.HasValue) query = query.Where(x => x.LastRequestedAt >= request.From);
        if (request.To.HasValue) query = query.Where(x => x.RequestedAt <= request.To);
        if (request.LocationId.HasValue) query = query.Where(x => x.LocationId == request.LocationId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            foreach (var word in request.Search.Trim().ToLowerInvariant()
                         .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(8))
            {
                var term = word;
                query = query.Where(x => x.ItemName.ToLower().Contains(term) ||
                                         (x.Notes != null && x.Notes.ToLower().Contains(term)) ||
                                         (x.CustomerName != null && x.CustomerName.ToLower().Contains(term)) ||
                                         (x.CustomerPhone != null && x.CustomerPhone.Contains(term)) ||
                                         db.Products.Any(p => p.Id == x.ProductId &&
                                                              (p.Code.ToLower().Contains(term) || p.NameEn.ToLower().Contains(term) || p.NameAr.ToLower().Contains(term))));
            }
        }

        var rows = await (
            from s in query
            join p in db.Products.AsNoTracking() on s.ProductId equals p.Id into products
            from p in products.DefaultIfEmpty()
            join l in db.Locations.AsNoTracking() on s.LocationId equals l.Id into locations
            from l in locations.DefaultIfEmpty()
            join u in db.Users.AsNoTracking() on s.CreatedBy equals u.Id into users
            from u in users.DefaultIfEmpty()
            select new
            {
                s,
                ProductCode = p != null ? p.Code : null,
                ProductNameEn = p != null ? p.NameEn : null,
                ProductNameAr = p != null ? p.NameAr : null,
                UnitsPerBox = p != null ? p.StripsPerBox * p.UnitsPerStrip : 1,
                LocationNameEn = l != null ? l.NameEn : null,
                LocationNameAr = l != null ? l.NameAr : null,
                UserEn = u != null ? u.FullNameEn : null,
                UserAr = u != null ? u.FullNameAr : null
            })
            .OrderByDescending(x => x.s.Status == ShortageStatuses.Open || x.s.Status == ShortageStatuses.Ordered)
            .ThenByDescending(x => x.s.RequestCount)
            .ThenByDescending(x => x.s.LastRequestedAt)
            .Take(1000)
            .ToListAsync(cancellationToken);

        // Current sellable stock (expired batches excluded), for the report's "on hand" column.
        var productIds = rows.Where(x => x.s.ProductId.HasValue).Select(x => x.s.ProductId!.Value).Distinct().ToList();
        var today = PharmacyClock.Today();
        var stock = productIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await db.ProductBatches.AsNoTracking()
                .Where(x => productIds.Contains(x.ProductId) && (x.ExpiryDate == null || x.ExpiryDate > today))
                .GroupBy(x => x.ProductId)
                .Select(g => new { g.Key, Qty = g.Sum(x => x.AvailableQuantity) })
                .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);

        return rows.Select(x => new ShortageDto(
                x.s.Id,
                x.s.ProductId,
                x.ProductCode,
                x.s.ItemName,
                x.ProductNameEn,
                x.ProductNameAr,
                x.s.LocationId,
                x.LocationNameEn,
                x.LocationNameAr,
                x.s.Quantity,
                x.s.RequestCount,
                x.s.Status,
                x.s.Notes,
                x.s.CustomerName,
                x.s.CustomerPhone,
                x.s.Source,
                x.s.RequestedAt,
                x.s.LastRequestedAt,
                x.s.ResolvedAt,
                x.UserEn,
                x.UserAr,
                x.s.ProductId.HasValue
                    ? Math.Round(stock.GetValueOrDefault(x.s.ProductId.Value) / Math.Max(1, x.UnitsPerBox), 2)
                    : null))
            .ToList();
    }
}
