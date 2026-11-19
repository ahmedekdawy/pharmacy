using Pharmacy.Shared.Localization;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Application.Catalog.Products.Commands.CreateProduct;
using Pharmacy.Application.Customers;
using Pharmacy.Application.Inventory;
using Pharmacy.Application.Inventory.Commands.CreateOpeningBalance;
using Pharmacy.Application.Sales;
using Pharmacy.Domain.Identity;

namespace Pharmacy.Application.Sync;

// ---------------------------------------------------------------- pull

public sealed record SyncProductDto(
    Guid Id,
    string Code,
    string? Barcode,
    string NameEn,
    string NameAr,
    decimal SellingPrice,
    int StripsPerBox,
    int UnitsPerStrip,
    bool AllowStripSale,
    bool AllowUnitSale,
    decimal? StripPrice,
    decimal? UnitPrice,
    decimal Stock);

public sealed record SyncLocationDto(Guid Id, string NameEn, string NameAr);

public sealed record SyncSnapshotDto(
    DateTimeOffset ServerTime,
    IReadOnlyList<SyncProductDto> Products,
    IReadOnlyList<CustomerDto> Customers,
    IReadOnlyList<SyncLocationDto> Locations);

/// <summary>Everything a POS device needs to keep selling while offline.</summary>
public sealed record GetSyncSnapshotQuery : IRequest<SyncSnapshotDto>;

public sealed class GetSyncSnapshotQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant, ISender sender)
    : IRequestHandler<GetSyncSnapshotQuery, SyncSnapshotDto>
{
    public async Task<SyncSnapshotDto> Handle(GetSyncSnapshotQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        // Only sellable stock: expired batches must not let the POS think the item is available.
        var today = PharmacyClock.Today();
        var stock = await db.ProductBatches.AsNoTracking()
            .Where(x => x.ExpiryDate == null || x.ExpiryDate > today)
            .GroupBy(x => x.ProductId)
            .Select(g => new { g.Key, Qty = g.Sum(x => x.AvailableQuantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);

        var products = (await db.Products.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.NameEn)
                .ToListAsync(cancellationToken))
            .Select(x => new SyncProductDto(
                x.Id, x.Code, x.Barcode, x.NameEn, x.NameAr, x.SellingPrice,
                x.StripsPerBox, x.UnitsPerStrip, x.AllowStripSale, x.AllowUnitSale, x.StripPrice, x.UnitPrice,
                stock.GetValueOrDefault(x.Id)))
            .ToList();

        var customers = (await sender.Send(new GetCustomersQuery(), cancellationToken))
            .Where(x => x.IsActive)
            .ToList();

        var locations = await db.Locations.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.NameEn)
            .Select(x => new SyncLocationDto(x.Id, x.NameEn, x.NameAr))
            .ToListAsync(cancellationToken);

        return new SyncSnapshotDto(DateTimeOffset.UtcNow, products, customers, locations);
    }
}

// ---------------------------------------------------------------- push

public static class SyncOperationTypes
{
    public const string SaleCreate = "sale.create";
    public const string CustomerCreate = "customer.create";
    public const string CustomerPaymentCreate = "customerPayment.create";
    public const string ProductCreate = "product.create";
    public const string OpeningBalanceCreate = "openingBalance.create";
    public const string ShortageCreate = "shortage.create";
}

public sealed record SyncOperationInput(Guid OpId, string Type, JsonElement Payload);

public sealed record SyncOperationResult(
    Guid OpId,
    string Status, // applied | duplicate | failed
    Guid? EntityId,
    string? Number,
    IReadOnlyList<string> Warnings,
    string? Error);

/// <param name="Permissions">Permissions of the calling user (filled by the API, not by the client).</param>
public sealed record PushSyncCommand(
    IReadOnlyList<SyncOperationInput> Operations,
    string? DeviceId,
    IReadOnlyCollection<string> Permissions) : IRequest<IReadOnlyList<SyncOperationResult>>;

public sealed class PushSyncCommandValidator : AbstractValidator<PushSyncCommand>
{
    public PushSyncCommandValidator()
    {
        RuleFor(x => x.Operations).NotNull();
        RuleFor(x => x.Operations.Count).LessThanOrEqualTo(200).When(x => x.Operations is not null);
        RuleFor(x => x.DeviceId).MaximumLength(64);
    }
}

/// <summary>
/// Applies operations recorded on a device (possibly offline) in order. Each operation is
/// idempotent (keyed by the entity id generated on the device), so re-sending is safe.
/// One failing operation does not block the others.
/// </summary>
public sealed class PushSyncCommandHandler(IPharmacyDbContext db, ISender sender)
    : IRequestHandler<PushSyncCommand, IReadOnlyList<SyncOperationResult>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<SyncOperationResult>> Handle(PushSyncCommand request, CancellationToken cancellationToken)
    {
        var results = new List<SyncOperationResult>(request.Operations.Count);

        foreach (var op in request.Operations)
        {
            try
            {
                results.Add(await ApplyAsync(op, request, cancellationToken));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ValidationException or JsonException or UnauthorizedAccessException or DbUpdateException)
            {
                db.ClearChanges();
                var message = ex is ValidationException ve
                    ? string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))
                    : ex is DbUpdateException ? "Could not be saved (conflict)." : ex.Message;
                results.Add(new SyncOperationResult(op.OpId, "failed", null, null, [], ErrorMessages.Localize(message)));
            }
        }

        return results;
    }

    private async Task<SyncOperationResult> ApplyAsync(SyncOperationInput op, PushSyncCommand request, CancellationToken ct)
    {
        switch (op.Type)
        {
            case SyncOperationTypes.SaleCreate:
            {
                Require(request, PermissionCodes.SaleCreate);
                var command = Deserialize<CreateSaleCommand>(op) with { DeviceId = request.DeviceId };
                var result = await sender.Send(command, ct);
                return new SyncOperationResult(op.OpId, result.AlreadyExisted ? "duplicate" : "applied",
                    result.Id, result.Number, ErrorMessages.LocalizeAll(result.Warnings), null);
            }
            case SyncOperationTypes.CustomerCreate:
            {
                Require(request, PermissionCodes.CustomerManage);
                var command = Deserialize<CreateCustomerCommand>(op);
                var id = await sender.Send(command, ct);
                return new SyncOperationResult(op.OpId, "applied", id, null, [], null);
            }
            case SyncOperationTypes.CustomerPaymentCreate:
            {
                Require(request, PermissionCodes.CustomerPayment);
                var command = Deserialize<CreateCustomerPaymentCommand>(op) with { DeviceId = request.DeviceId };
                var result = await sender.Send(command, ct);
                return new SyncOperationResult(op.OpId, result.AlreadyExisted ? "duplicate" : "applied",
                    result.Id, result.Number, ErrorMessages.LocalizeAll(result.Warnings), null);
            }
            case SyncOperationTypes.ProductCreate:
            {
                Require(request, PermissionCodes.ProductCreate);
                var command = Deserialize<CreateProductCommand>(op);
                var id = await sender.Send(command, ct);
                return new SyncOperationResult(op.OpId, "applied", id, null, [], null);
            }
            case SyncOperationTypes.OpeningBalanceCreate:
            {
                Require(request, PermissionCodes.InventoryAdjust);
                var command = Deserialize<CreateOpeningBalanceCommand>(op);
                var id = await sender.Send(command, ct);
                return new SyncOperationResult(op.OpId, "applied", id, null, [], null);
            }
            case SyncOperationTypes.ShortageCreate:
            {
                Require(request, PermissionCodes.ShortageCreate);
                var command = Deserialize<CreateShortageCommand>(op);
                var result = await sender.Send(command, ct);
                return new SyncOperationResult(op.OpId, "applied", result.Id, null, [], null);
            }
            default:
                throw new InvalidOperationException($"Unknown operation type '{op.Type}'.");
        }
    }

    private static T Deserialize<T>(SyncOperationInput op) =>
        op.Payload.Deserialize<T>(Json) ?? throw new InvalidOperationException("Empty payload.");

    private static void Require(PushSyncCommand request, string permission)
    {
        if (!request.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"Missing permission {permission}.");
    }
}
