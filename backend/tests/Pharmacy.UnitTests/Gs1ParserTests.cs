using Pharmacy.Application.Catalog.Barcodes;

namespace Pharmacy.UnitTests;

public class Gs1ParserTests
{
    private const char Gs = '\u001d';

    [Fact]
    public void Parses_DataMatrix_With_Group_Separators()
    {
        var raw = $"01062212345678901728123110LOT-77{Gs}21SN000123";
        var d = Gs1Parser.Parse(raw);
        Assert.Equal("06221234567890", d.Gtin);
        Assert.Equal(new DateOnly(2028, 12, 31), d.Expiry);
        Assert.Equal("LOT-77", d.Batch);
        Assert.Equal("SN000123", d.Serial);
    }

    [Fact]
    public void Parses_Symbology_Prefix_And_Day_Zero()
    {
        var d = Gs1Parser.Parse("]d2010622123456789017270600");
        Assert.Equal("06221234567890", d.Gtin);
        Assert.Equal(new DateOnly(2027, 6, 30), d.Expiry); // day 00 = end of month
    }

    [Fact]
    public void Parses_Human_Readable_Form()
    {
        var d = Gs1Parser.Parse("(01)06221234567890(17)290115(10)B12");
        Assert.Equal("06221234567890", d.Gtin);
        Assert.Equal(new DateOnly(2029, 1, 15), d.Expiry);
        Assert.Equal("B12", d.Batch);
    }

    [Fact]
    public void Normalizes_Gtin14_To_Printed_Ean13()
    {
        Assert.Equal("6221234567890", Gs1Parser.Normalize("01062212345678901728123110LOT"));
        Assert.Equal("6221234567890", Gs1Parser.Normalize("6221234567890"));
        Assert.Contains("06221234567890", Gs1Parser.Candidates("6221234567890"));
    }

    [Fact]
    public void Plain_Ean13_Has_No_Gs1_Data()
    {
        var d = Gs1Parser.Parse("6221234567890");
        Assert.Null(d.Gtin);
        Assert.Null(d.Expiry);
    }
}
