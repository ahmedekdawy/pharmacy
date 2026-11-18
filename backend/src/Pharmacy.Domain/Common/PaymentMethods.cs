namespace Pharmacy.Domain.Common;

public static class PaymentMethods
{
    public const string Cash = "Cash";
    public const string Card = "Card";
    public const string Wallet = "Wallet";

    public static readonly string[] All = [Cash, Card, Wallet];

    public static string Normalize(string? value) =>
        All.FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)) ?? Cash;
}
