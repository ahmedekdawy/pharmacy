using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Expenses;

public class Expense : TenantOwnedEntity
{
    public Guid LocationId { get; set; }
    public string Category { get; set; } = default!;
    public string DescriptionEn { get; set; } = default!;
    public string DescriptionAr { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateTimeOffset SpentAt { get; set; }
    public Guid? CashShiftId { get; set; }
}
