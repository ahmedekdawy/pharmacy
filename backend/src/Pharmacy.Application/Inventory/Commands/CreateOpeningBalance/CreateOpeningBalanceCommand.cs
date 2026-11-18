using MediatR;

namespace Pharmacy.Application.Inventory.Commands.CreateOpeningBalance;

public sealed record CreateOpeningBalanceCommand(
    Guid LocationId,
    Guid ProductId,
    string BatchNumber,
    DateOnly? ExpiryDate,
    decimal PurchasePrice,
    decimal Quantity,
    Pharmacy.Domain.Catalog.SaleUnit Unit = Pharmacy.Domain.Catalog.SaleUnit.Box,
    Guid? Id = null) : IRequest<Guid>;
