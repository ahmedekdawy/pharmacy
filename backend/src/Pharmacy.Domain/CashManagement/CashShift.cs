using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.CashManagement;

public class CashShift : TenantOwnedEntity
{
    public Guid LocationId { get; set; }
    public Guid OpenedByUserId { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal? ClosingCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public string Status { get; set; } = "Open";
    public string? Notes { get; set; }
}
