using System.Globalization;
using Pharmacy.Application.Common;
using Pharmacy.Application.Sales;
using Pharmacy.Shared.Localization;

namespace Pharmacy.UnitTests;

public class SaleRulesTests
{
    private static CreateSaleCommand Sale(decimal? unitPrice, decimal discount = 0) => new(
        Guid.NewGuid(), null, null,
        [new SaleLineInput(Guid.NewGuid(), null, 1, unitPrice)],
        DiscountAmount: discount);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validator_rejects_zero_or_negative_unit_price(decimal price)
    {
        var result = new CreateSaleCommandValidator().Validate(Sale(price));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Unit price must be greater than zero.");
    }

    [Fact]
    public void Validator_accepts_positive_or_catalog_price()
    {
        Assert.True(new CreateSaleCommandValidator().Validate(Sale(32)).IsValid);
        Assert.True(new CreateSaleCommandValidator().Validate(Sale(null)).IsValid);
    }

    [Fact]
    public void Batch_expiring_today_is_expired()
    {
        var today = new DateOnly(2026, 11, 19);
        Assert.True(PharmacyClock.IsExpired(today, today));
        Assert.True(PharmacyClock.IsExpired(today.AddDays(-1), today));
        Assert.False(PharmacyClock.IsExpired(today.AddDays(1), today));
        Assert.False(PharmacyClock.IsExpired(null, today));
    }

    [Theory]
    [InlineData("Discount cannot exceed the invoice subtotal.", "لا يمكن أن يتجاوز الخصم إجمالي الفاتورة.")]
    [InlineData("Invoice net total must be greater than zero.", "يجب أن يكون صافي الفاتورة أكبر من صفر.")]
    [InlineData("Only expired stock is left for product EG-2MWHITES.", "المتاح من الصنف EG-2MWHITES منتهي الصلاحية ولا يمكن بيعه.")]
    [InlineData("EG-2MWHITES: sold from an expired batch.", "EG-2MWHITES: تم البيع من تشغيلة منتهية الصلاحية.")]
    public void New_sale_errors_are_translated_to_arabic(string english, string arabic)
    {
        Assert.Equal(arabic, ErrorMessages.Localize(english, new CultureInfo("ar-EG")));
    }
}
