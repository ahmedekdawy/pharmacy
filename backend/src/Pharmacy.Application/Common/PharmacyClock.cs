namespace Pharmacy.Application.Common;

/// <summary>
/// The pharmacy's local calendar day. Pharmacies run on Cairo time; using the UTC date would
/// shift "today" by 2–3 hours (e.g. a batch expiring today would still be sellable after midnight).
/// </summary>
public static class PharmacyClock
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).DateTime);

    /// <summary>A batch can be sold only while its expiry date is after today.</summary>
    public static bool IsExpired(DateOnly? expiryDate, DateOnly today) => expiryDate.HasValue && expiryDate.Value <= today;

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Africa/Cairo", "Egypt Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // try the next id
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Cairo", TimeSpan.FromHours(2), "Cairo", "Cairo");
    }
}
