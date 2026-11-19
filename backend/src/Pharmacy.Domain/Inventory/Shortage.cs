using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Inventory;

/// <summary>
/// An item the pharmacy ran out of or was asked for and does not stock (النواقص).
/// Repeated requests for the same item while it is still open are merged: the quantity grows and
/// <see cref="RequestCount"/> shows how often customers asked for it.
/// </summary>
public class Shortage : TenantOwnedEntity
{
    /// <summary>Catalog product, when the item exists in the catalog. Null for free-text items.</summary>
    public Guid? ProductId { get; set; }

    /// <summary>Name as typed (or the product's name). Always filled so the report reads well without a product.</summary>
    public string ItemName { get; set; } = default!;

    public Guid? LocationId { get; set; }

    /// <summary>Requested quantity in boxes.</summary>
    public decimal Quantity { get; set; } = 1;

    public int RequestCount { get; set; } = 1;
    public string Status { get; set; } = ShortageStatuses.Open;
    public string? Notes { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }

    /// <summary>Manual (shortages page) or POS (added while selling).</summary>
    public string Source { get; set; } = "Manual";

    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset LastRequestedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

public static class ShortageStatuses
{
    public const string Open = "Open";
    public const string Ordered = "Ordered";
    public const string Received = "Received";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Open, Ordered, Received, Cancelled];

    /// <summary>Still needs attention (not received / cancelled).</summary>
    public static bool IsActive(string status) => status is Open or Ordered;

    public static string? Normalize(string? value) =>
        All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
