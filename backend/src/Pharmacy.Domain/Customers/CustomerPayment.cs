using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Customers;

/// <summary>A payment collected from a customer against their outstanding debt (سداد مديونية).</summary>
public class CustomerPayment : TenantOwnedEntity
{
    public string Number { get; set; } = default!;
    public Guid CustomerId { get; set; }
    public Guid? LocationId { get; set; }
    public DateTimeOffset PaidAt { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;
    public string? Notes { get; set; }
    public string? DeviceId { get; set; }
}
