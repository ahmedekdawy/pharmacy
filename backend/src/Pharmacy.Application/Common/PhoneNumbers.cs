namespace Pharmacy.Application.Common;

public static class PhoneNumbers
{
    /// <summary>Strips everything but digits so phone numbers compare consistently regardless of formatting.</summary>
    public static string? Normalize(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }
}
