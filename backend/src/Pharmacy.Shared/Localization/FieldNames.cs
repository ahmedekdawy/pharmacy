using System.Globalization;

namespace Pharmacy.Shared.Localization;

/// <summary>Arabic display names for validated request properties, used in validation messages.</summary>
public static class FieldNames
{
    private static readonly Dictionary<string, string> Arabic = new(StringComparer.Ordinal)
    {
        ["Address"] = "العنوان",
        ["AdminEmail"] = "البريد الإلكتروني للمسؤول",
        ["AdminFullNameAr"] = "اسم المسؤول (عربي)",
        ["AdminFullNameEn"] = "اسم المسؤول (إنجليزي)",
        ["AdminPassword"] = "كلمة مرور المسؤول",
        ["AdminPhoneNumber"] = "هاتف المسؤول",
        ["Amount"] = "المبلغ",
        ["Barcode"] = "الباركود",
        ["BatchId"] = "التشغيلة",
        ["BatchNumber"] = "رقم التشغيلة",
        ["CashShiftId"] = "الوردية",
        ["Category"] = "الفئة",
        ["ClosingCash"] = "نقدية الإغلاق",
        ["Code"] = "الكود",
        ["CreditLimit"] = "حد الائتمان",
        ["CustomerId"] = "العميل",
        ["DescriptionAr"] = "الوصف (عربي)",
        ["DescriptionEn"] = "الوصف (إنجليزي)",
        ["DeviceId"] = "الجهاز",
        ["DiscountAmount"] = "الخصم",
        ["Email"] = "البريد الإلكتروني",
        ["EmailOrMobile"] = "البريد الإلكتروني أو الموبايل",
        ["FromLocationId"] = "من فرع",
        ["FullNameAr"] = "الاسم (عربي)",
        ["FullNameEn"] = "الاسم (إنجليزي)",
        ["Id"] = "المعرّف",
        ["Items"] = "الأصناف",
        ["Key"] = "المفتاح",
        ["LocationId"] = "الفرع",
        ["NameAr"] = "الاسم (عربي)",
        ["NameEn"] = "الاسم (إنجليزي)",
        ["Notes"] = "الملاحظات",
        ["Number"] = "الرقم",
        ["OpeningCash"] = "نقدية الافتتاح",
        ["Operations"] = "العمليات",
        ["PaidAmount"] = "المبلغ المدفوع",
        ["Password"] = "كلمة المرور",
        ["PhoneNumber"] = "رقم الهاتف",
        ["ProductId"] = "الصنف",
        ["PurchasePrice"] = "سعر الشراء",
        ["Quantity"] = "الكمية",
        ["QuantityDelta"] = "تغيير الكمية",
        ["SaleId"] = "الفاتورة",
        ["SaleItemId"] = "بند الفاتورة",
        ["SellingPrice"] = "سعر البيع",
        ["StripPrice"] = "سعر الشريط",
        ["StripsPerBox"] = "عدد الشرائط في العلبة",
        ["SupplierId"] = "المورد",
        ["TenantCode"] = "كود الصيدلية",
        ["TenantNameAr"] = "اسم الصيدلية (عربي)",
        ["TenantNameEn"] = "اسم الصيدلية (إنجليزي)",
        ["ToLocationId"] = "إلى فرع",
        ["Type"] = "النوع",
        ["Unit"] = "الوحدة",
        ["UnitCost"] = "تكلفة الوحدة",
        ["UnitPrice"] = "سعر الوحدة",
        ["UnitsPerStrip"] = "عدد الأقراص في الشريط",
        ["UserId"] = "المستخدم",
        ["Value"] = "القيمة",
    };

    /// <summary>Arabic name for <paramref name="propertyName"/> when the UI culture is Arabic; otherwise null (use the default).</summary>
    public static string? Resolve(string? propertyName)
    {
        if (propertyName is null || CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "ar")
        {
            return null;
        }

        return Arabic.GetValueOrDefault(propertyName);
    }
}
