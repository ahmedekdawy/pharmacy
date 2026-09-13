namespace Pharmacy.Domain.Inventory;

public enum InventoryTransactionType
{
    Purchase = 1,
    Sale = 2,
    SaleReturn = 3,
    PurchaseReturn = 4,
    TransferIn = 5,
    TransferOut = 6,
    Adjustment = 7,
    Expired = 8,
    Damaged = 9,
    OpeningBalance = 10
}
