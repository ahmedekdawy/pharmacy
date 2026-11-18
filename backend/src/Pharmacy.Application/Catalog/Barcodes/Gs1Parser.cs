using System.Globalization;
using System.Text.RegularExpressions;

namespace Pharmacy.Application.Catalog.Barcodes;

public sealed record Gs1Data(string? Gtin, DateOnly? Expiry, string? Batch, string? Serial);

/// <summary>
/// Parses scanned codes. Medicine packs in Egypt (EDA track &amp; trace) carry a GS1 DataMatrix with
/// (01) GTIN, (17) expiry YYMMDD, (10) batch/lot and (21) serial. Plain EAN-13 / UPC codes are also accepted.
/// </summary>
public static partial class Gs1Parser
{
    private const char Gs = '\u001d';

    public static Gs1Data Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new Gs1Data(null, null, null, null);
        var code = raw.Trim();

        // Strip AIM symbology identifiers such as "]d2", "]C1", "]Q3".
        if (code.Length > 3 && code[0] == ']') code = code[3..];

        if (code.Contains('('))
            return ParseHumanReadable(code);

        // Plain linear barcode (EAN-8/12/13, GTIN-14) or internal code.
        if (!code.StartsWith("01", StringComparison.Ordinal) || code.Length < 16 || !IsDigits(code.AsSpan(2, 14)))
            return new Gs1Data(null, null, null, null);

        return ParseElementString(code);
    }

    /// <summary>The barcode to store/look up: GTIN-14 with a leading 0 becomes the printed EAN-13.</summary>
    public static string Normalize(string? raw)
    {
        var gs1 = Parse(raw);
        var plain = raw?.Trim() ?? string.Empty;
        if (plain.Length > 3 && plain[0] == ']') plain = plain[3..];
        var value = gs1.Gtin ?? plain;
        value = value.Replace(Gs.ToString(), string.Empty).Trim();
        if (value.Length == 14 && value[0] == '0' && IsDigits(value)) value = value[1..];
        return value;
    }

    /// <summary>All forms a barcode may have been saved under (EAN-13 and GTIN-14).</summary>
    public static IReadOnlyList<string> Candidates(string? raw)
    {
        var normalized = Normalize(raw);
        var list = new List<string> { normalized };
        if (normalized.Length == 13 && IsDigits(normalized)) list.Add("0" + normalized);
        if (!string.IsNullOrWhiteSpace(raw) && !list.Contains(raw.Trim())) list.Add(raw.Trim());
        return list.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
    }

    private static Gs1Data ParseElementString(string code)
    {
        string? gtin = null, batch = null, serial = null;
        DateOnly? expiry = null;
        var i = 0;
        while (i < code.Length)
        {
            if (code[i] == Gs) { i++; continue; }
            if (i + 2 > code.Length) break;
            var ai = code.Substring(i, 2);
            i += 2;
            switch (ai)
            {
                case "01":
                    gtin = Take(code, ref i, 14);
                    break;
                case "17":
                case "15":
                case "11":
                    var date = Take(code, ref i, 6);
                    if (ai == "17") expiry = ParseDate(date);
                    break;
                case "10":
                    batch = TakeVariable(code, ref i, 20);
                    break;
                case "21":
                    serial = TakeVariable(code, ref i, 20);
                    break;
                default:
                    // Unknown AI: skip to the next group separator.
                    TakeVariable(code, ref i, 90);
                    break;
            }
        }

        return new Gs1Data(gtin, expiry, batch, serial);
    }

    private static Gs1Data ParseHumanReadable(string code)
    {
        string? gtin = null, batch = null, serial = null;
        DateOnly? expiry = null;
        foreach (Match m in HumanReadable().Matches(code))
        {
            var value = m.Groups["v"].Value.Trim();
            switch (m.Groups["ai"].Value)
            {
                case "01": gtin = value; break;
                case "17": expiry = ParseDate(value); break;
                case "10": batch = value; break;
                case "21": serial = value; break;
            }
        }

        return new Gs1Data(gtin, expiry, batch, serial);
    }

    private static string Take(string s, ref int i, int length)
    {
        var len = Math.Min(length, s.Length - i);
        var value = s.Substring(i, len);
        i += len;
        return value;
    }

    private static string TakeVariable(string s, ref int i, int max)
    {
        var end = s.IndexOf(Gs, i);
        if (end < 0) end = s.Length;
        end = Math.Min(end, i + max);
        var value = s[i..end];
        i = end;
        return value;
    }

    private static DateOnly? ParseDate(string yymmdd)
    {
        if (yymmdd.Length != 6 || !IsDigits(yymmdd)) return null;
        var year = 2000 + int.Parse(yymmdd[..2], CultureInfo.InvariantCulture);
        var month = int.Parse(yymmdd.Substring(2, 2), CultureInfo.InvariantCulture);
        var day = int.Parse(yymmdd.Substring(4, 2), CultureInfo.InvariantCulture);
        if (month is < 1 or > 12) return null;
        // GS1: day 00 means the last day of the month.
        var lastDay = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, day == 0 || day > lastDay ? lastDay : day);
    }

    private static bool IsDigits(ReadOnlySpan<char> s)
    {
        foreach (var c in s) if (!char.IsAsciiDigit(c)) return false;
        return s.Length > 0;
    }

    [GeneratedRegex(@"\((?<ai>\d{2,4})\)(?<v>[^(]*)")]
    private static partial Regex HumanReadable();
}
