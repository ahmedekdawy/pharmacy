using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public class Sale : TenantOwnedEntity
{
    public string Number { get; set; } = default!;
    public Guid LocationId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTimeOffset SoldAt { get; set; }

    /// <summary>Sum of line totals before discount.</summary>
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }

    /// <summary>Net invoice amount (SubTotal - Discount).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Amount paid at the time of sale.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>Amount left on the customer's account (آجل).</summary>
    public decimal DueAmount { get; set; }
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;

    public string Status { get; set; } = "Completed";
    public string? Notes { get; set; }

    /// <summary>"Online" or "Offline" (created on a device without connectivity, then synced).</summary>
    public string Source { get; set; } = "Online";
    public string? DeviceId { get; set; }
    public bool HasStockWarning { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}
