using System.Globalization;
using System.Text.RegularExpressions;

namespace Pharmacy.Shared.Localization;

/// <summary>
/// Translates the English error messages thrown by the application into the request's UI culture.
/// Handlers keep throwing plain English; this is applied where messages leave the API.
/// Unknown messages pass through unchanged.
/// </summary>
public static partial class ErrorMessages
{
    private static readonly Dictionary<string, string> Arabic = new(StringComparer.Ordinal)
    {
        ["A customer is required for credit (deferred) sales."] = "يجب اختيار العميل في البيع الآجل.",
        ["Account is deactivated."] = "الحساب موقوف.",
        ["An open cash shift already exists for this location."] = "توجد وردية نقدية مفتوحة بالفعل لهذا الفرع.",
        ["Barcode already exists for this tenant."] = "الباركود مستخدم بالفعل.",
        ["Batch not found."] = "التشغيلة غير موجودة.",
        ["Brand not found."] = "العلامة التجارية غير موجودة.",
        ["Cannot delete a customer with an outstanding balance."] = "لا يمكن حذف عميل عليه رصيد مستحق.",
        ["Cash shift is already closed."] = "الوردية النقدية مغلقة بالفعل.",
        ["Cash shift not found."] = "الوردية النقدية غير موجودة.",
        ["Category not found."] = "التصنيف غير موجود.",
        ["Customer code already exists."] = "كود العميل مستخدم بالفعل.",
        ["Customer has no outstanding balance."] = "لا يوجد رصيد مستحق على العميل.",
        ["Customer not found."] = "العميل غير موجود.",
        ["Email already exists for this tenant."] = "البريد الإلكتروني مستخدم بالفعل.",
        ["Empty payload."] = "لا توجد بيانات مرسلة.",
        ["Expense not found."] = "المصروف غير موجود.",
        ["Insufficient batch quantity for adjustment."] = "كمية التشغيلة غير كافية للتسوية.",
        ["Insufficient stock at source location."] = "المخزون غير كافٍ في الفرع المصدر.",
        ["Insufficient stock for adjustment."] = "المخزون غير كافٍ للتسوية.",
        ["Invalid tenant or credentials."] = "كود الصيدلية أو بيانات الدخول غير صحيحة.",
        ["Location not found."] = "الفرع غير موجود.",
        ["Open cash shift not found for location."] = "لا توجد وردية نقدية مفتوحة لهذا الفرع.",
        ["Owner role cannot be deleted."] = "لا يمكن حذف دور المالك.",
        ["Phone number already exists for this tenant."] = "رقم الهاتف مستخدم بالفعل.",
        ["Product code already exists for this tenant."] = "كود الصنف مستخدم بالفعل.",
        ["Product not found."] = "الصنف غير موجود.",
        ["Role code already exists."] = "كود الدور مستخدم بالفعل.",
        ["Role is assigned to users and cannot be deleted."] = "الدور مُسند لمستخدمين ولا يمكن حذفه.",
        ["Role not found."] = "الدور غير موجود.",
        ["Sale item not found."] = "بند الفاتورة غير موجود.",
        ["Sale not found."] = "الفاتورة غير موجودة.",
        ["Setting not found."] = "الإعداد غير موجود.",
        ["Supplier code already exists."] = "كود المورد مستخدم بالفعل.",
        ["Supplier not found."] = "المورد غير موجود.",
        ["Tenant code already exists."] = "كود الصيدلية مستخدم بالفعل.",
        ["Tenant is deactivated."] = "الصيدلية موقوفة.",
        ["Tenant is required."] = "يجب تحديد الصيدلية.",
        ["Tenant not found."] = "الصيدلية غير موجودة.",
        ["User is required."] = "يجب تسجيل الدخول.",
        ["User not found."] = "المستخدم غير موجود.",
        ["You cannot deactivate your own account."] = "لا يمكنك إيقاف حسابك.",
        ["You cannot delete your own account."] = "لا يمكنك حذف حسابك.",
        ["Could not be saved (conflict)."] = "تعذر الحفظ (تعارض في البيانات).",
        ["Tenant header does not match token tenant."] = "الصيدلية المرسلة لا تطابق صيدلية المستخدم.",
        ["UnexpectedError"] = "حدث خطأ غير متوقع. حاول مرة أخرى.",
    };

    // Messages with values inserted: English pattern -> Arabic template using the captured groups.
    private static readonly (Regex Pattern, string Arabic)[] ArabicTemplates =
    [
        (InsufficientStockRegex(), "المخزون غير كافٍ للصنف ${code}."),
        (MissingPermissionRegex(), "ليس لديك صلاحية ${permission}."),
        (CannotSellByUnitRegex(), "الصنف ${code} لا يُباع بال${unit}."),
        (InactiveProductRegex(), "الصنف ${code} غير نشط."),
        (ReturnExceedsRegex(), "كمية المرتجع أكبر من الكمية المتبقية للبند ${item}."),
        (UnknownOperationRegex(), "نوع عملية غير معروف '${type}'."),
        (CreditLimitRegex(), "تم تجاوز حد الائتمان للعميل ${code} (الحد ${limit}، الرصيد ${balance})."),
        (SoldByDisabledUnitRegex(), "${code}: تم البيع بال${unit} رغم أنها غير مفعّلة."),
        (StockShortRegex(), "${code}: عجز في المخزون بمقدار ${qty} وحدة."),
        (PaymentExceedsRegex(), "المبلغ المدفوع ${amount} أكبر من الرصيد المستحق ${balance}؛ سيصبح للعميل رصيد دائن."),
    ];

    private static readonly Dictionary<string, string> ArabicUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["box"] = "علبة",
        ["strip"] = "شريط",
        ["unit"] = "قرص",
        ["pill"] = "قرص",
    };

    /// <summary>Translates <paramref name="message"/> into the current UI culture when a translation exists.</summary>
    public static string Localize(string message) => Localize(message, CultureInfo.CurrentUICulture);

    public static string Localize(string message, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(message) || culture.TwoLetterISOLanguageName != "ar")
        {
            return message == "UnexpectedError" ? "An unexpected error occurred. Please try again." : message;
        }

        if (Arabic.TryGetValue(message, out var translated))
        {
            return translated;
        }

        foreach (var (pattern, template) in ArabicTemplates)
        {
            var match = pattern.Match(message);
            if (!match.Success)
            {
                continue;
            }

            if (match.Groups["unit"].Success && ArabicUnits.TryGetValue(match.Groups["unit"].Value, out var unit))
            {
                return pattern.Replace(message, template.Replace("${unit}", unit));
            }

            return pattern.Replace(message, template);
        }

        return message;
    }

    [GeneratedRegex(@"^Insufficient stock for product (?<code>.+)\.$")]
    private static partial Regex InsufficientStockRegex();

    [GeneratedRegex(@"^Missing permission (?<permission>.+)\.$")]
    private static partial Regex MissingPermissionRegex();

    [GeneratedRegex(@"^Product (?<code>.+) cannot be sold by (?<unit>\w+)\.$")]
    private static partial Regex CannotSellByUnitRegex();

    [GeneratedRegex(@"^Product (?<code>.+) is inactive\.$")]
    private static partial Regex InactiveProductRegex();

    [GeneratedRegex(@"^Return quantity exceeds remaining quantity for item (?<item>.+)\.$")]
    private static partial Regex ReturnExceedsRegex();

    [GeneratedRegex(@"^Unknown operation type '(?<type>.*)'\.$")]
    private static partial Regex UnknownOperationRegex();

    [GeneratedRegex(@"^Credit limit exceeded for customer (?<code>.+) \(limit (?<limit>[\d.]+), balance (?<balance>-?[\d.]+)\)\.$")]
    private static partial Regex CreditLimitRegex();

    [GeneratedRegex(@"^(?<code>.+): sold by (?<unit>\w+) although it is not enabled\.$")]
    private static partial Regex SoldByDisabledUnitRegex();

    [GeneratedRegex(@"^(?<code>.+): stock short by (?<qty>[\d.]+) unit\(s\)\.$")]
    private static partial Regex StockShortRegex();

    [GeneratedRegex(@"^Payment (?<amount>[\d.]+) exceeds outstanding balance (?<balance>-?[\d.]+); the customer will have a credit\.$")]
    private static partial Regex PaymentExceedsRegex();

    /// <summary>Localizes each message in a list (e.g. warnings returned to the client).</summary>
    public static IReadOnlyList<string> LocalizeAll(IEnumerable<string> messages) => messages.Select(m => Localize(m)).ToList();
}
