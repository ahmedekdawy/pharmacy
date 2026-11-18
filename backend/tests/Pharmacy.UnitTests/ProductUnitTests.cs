using Pharmacy.Domain.Catalog;

namespace Pharmacy.UnitTests;

public class ProductUnitTests
{
    // Box of 3 strips × 10 pills, box price 90
    private static Product Panadol() => new()
    {
        Code = "PAN",
        NameAr = "بنادول",
        NameEn = "Panadol",
        SellingPrice = 90m,
        StripsPerBox = 3,
        UnitsPerStrip = 10,
        AllowStripSale = true,
        AllowUnitSale = true
    };

    [Fact]
    public void Factors_Are_Expressed_In_Smallest_Units()
    {
        var p = Panadol();
        Assert.Equal(30, p.UnitsPerBox);
        Assert.Equal(30, p.FactorFor(SaleUnit.Box));
        Assert.Equal(10, p.FactorFor(SaleUnit.Strip));
        Assert.Equal(1, p.FactorFor(SaleUnit.Unit));
    }

    [Fact]
    public void Prices_Are_Derived_From_Box_Price_When_Not_Set()
    {
        var p = Panadol();
        Assert.Equal(90m, p.PriceFor(SaleUnit.Box));
        Assert.Equal(30m, p.PriceFor(SaleUnit.Strip));
        Assert.Equal(3m, p.PriceFor(SaleUnit.Unit));
    }

    [Fact]
    public void Explicit_Strip_And_Unit_Prices_Win()
    {
        var p = Panadol();
        p.StripPrice = 32m;
        p.UnitPrice = 3.5m;
        Assert.Equal(32m, p.PriceFor(SaleUnit.Strip));
        Assert.Equal(3.5m, p.PriceFor(SaleUnit.Unit));
    }

    [Fact]
    public void Retail_Sale_Must_Be_Enabled()
    {
        var p = Panadol();
        p.AllowUnitSale = false;
        Assert.True(p.CanSellIn(SaleUnit.Box));
        Assert.True(p.CanSellIn(SaleUnit.Strip));
        Assert.False(p.CanSellIn(SaleUnit.Unit));
    }

    [Fact]
    public void Defaults_Keep_Legacy_Products_Per_Box()
    {
        var p = new Product { Code = "X", NameAr = "x", NameEn = "x", SellingPrice = 10m };
        Assert.Equal(1, p.UnitsPerBox);
        Assert.Equal(1, p.FactorFor(SaleUnit.Box));
        Assert.False(p.CanSellIn(SaleUnit.Strip));
    }
}
