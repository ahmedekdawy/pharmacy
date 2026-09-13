import { Injectable, computed, signal } from '@angular/core';

export type AppLanguage = 'en' | 'ar';

const LANG_KEY = 'pharmacy.language';

@Injectable({ providedIn: 'root' })
export class LocalizationService {
  readonly language = signal<AppLanguage>(this.readLanguage());
  readonly direction = computed(() => (this.language() === 'ar' ? 'rtl' : 'ltr'));
  readonly isRtl = computed(() => this.language() === 'ar');

  constructor() {
    this.apply(this.language());
  }

  toggleLanguage(): void {
    this.setLanguage(this.language() === 'en' ? 'ar' : 'en');
  }

  setLanguage(lang: AppLanguage): void {
    this.language.set(lang);
    localStorage.setItem(LANG_KEY, lang);
    this.apply(lang);
  }

  translate(key: string): string {
    const dict = this.language() === 'ar' ? ar : en;
    return dict[key] ?? key;
  }

  private apply(lang: AppLanguage): void {
    document.documentElement.lang = lang;
    document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
  }

  private readLanguage(): AppLanguage {
    const stored = localStorage.getItem(LANG_KEY);
    return stored === 'ar' ? 'ar' : 'en';
  }
}

const en: Record<string, string> = {
  'COMMON.SAVE': 'Save',
  'COMMON.CANCEL': 'Cancel',
  'COMMON.DELETE': 'Delete',
  'COMMON.LOGOUT': 'Sign out',
  'COMMON.EXPAND': 'Expand menu',
  'COMMON.COLLAPSE': 'Collapse menu',
  'COMMON.LANGUAGE': 'Language',
  'COMMON.THEME': 'Theme',
  'COMMON.THEME_LIGHT': 'Light',
  'COMMON.THEME_DARK': 'Dark',
  'COMMON.MENU': 'Navigation',
  'BRAND.NAME': 'Pharmacy',
  'BRAND.TAGLINE': 'Operations suite',
  'LANDING.HEADLINE': 'Run your pharmacy with clarity',
  'LANDING.SUBTITLE': 'Multi-branch inventory, expiry control, and bilingual POS in one platform.',
  'LANDING.CTA_TRIAL': 'Start Free Trial',
  'LANDING.CTA_DEMO': 'Request Demo',
  'LANDING.LOGIN': 'Login',
  'APP.DASHBOARD': 'Dashboard',
  'APP.PRODUCTS': 'Products',
  'APP.CATEGORIES': 'Categories',
  'APP.BRANDS': 'Brands',
  'APP.LOCATIONS': 'Locations',
  'APP.INVENTORY': 'Inventory',
  'APP.USERS': 'Users',
  'APP.ROLES': 'Roles',
  'COMMON.LOADING': 'Loading…',
  'PRODUCTS.CATALOG_HINT': 'Search the Egyptian drug database, select a drug, edit details, then save to your list.',
  'PRODUCTS.SEARCH_DRUGS': 'Egyptian drug catalog',
  'PRODUCTS.SEARCH_PLACEHOLDER': 'Search by name, scientific name, or manufacturer…',
  'PRODUCTS.NO_MATCHES': 'No matching drugs found.',
  'PRODUCTS.SELECTED': 'Selected from catalog — edit before saving',
  'PRODUCTS.EDIT_BEFORE_SAVE': 'Review and edit product fields before saving.',
  'PRODUCTS.CODE': 'Code',
  'PRODUCTS.NAME_EN': 'Name (EN)',
  'PRODUCTS.NAME_AR': 'Name (AR)',
  'PRODUCTS.BARCODE': 'Barcode',
  'PRODUCTS.PRICE': 'Price'
};

const ar: Record<string, string> = {
  'COMMON.SAVE': 'حفظ',
  'COMMON.CANCEL': 'إلغاء',
  'COMMON.DELETE': 'حذف',
  'COMMON.LOGOUT': 'تسجيل الخروج',
  'COMMON.EXPAND': 'توسيع القائمة',
  'COMMON.COLLAPSE': 'طي القائمة',
  'COMMON.LANGUAGE': 'اللغة',
  'COMMON.THEME': 'المظهر',
  'COMMON.THEME_LIGHT': 'فاتح',
  'COMMON.THEME_DARK': 'داكن',
  'COMMON.MENU': 'القائمة',
  'BRAND.NAME': 'صيدلية',
  'BRAND.TAGLINE': 'منصة التشغيل',
  'LANDING.HEADLINE': 'أدر صيدليتك بوضوح',
  'LANDING.SUBTITLE': 'مخزون متعدد الفروع، التحكم في الصلاحية، ونقاط بيع ثنائية اللغة في منصة واحدة.',
  'LANDING.CTA_TRIAL': 'ابدأ تجربة مجانية',
  'LANDING.CTA_DEMO': 'اطلب عرضًا',
  'LANDING.LOGIN': 'تسجيل الدخول',
  'APP.DASHBOARD': 'لوحة التحكم',
  'APP.PRODUCTS': 'المنتجات',
  'APP.CATEGORIES': 'التصنيفات',
  'APP.BRANDS': 'العلامات',
  'APP.LOCATIONS': 'المواقع',
  'APP.INVENTORY': 'المخزون',
  'APP.USERS': 'المستخدمون',
  'APP.ROLES': 'الأدوار',
  'COMMON.LOADING': 'جارٍ التحميل…',
  'PRODUCTS.CATALOG_HINT': 'ابحث في قاعدة الأدوية المصرية، اختر دواءً، عدّل البيانات، ثم احفظه في قائمتك.',
  'PRODUCTS.SEARCH_DRUGS': 'قاعدة الأدوية المصرية',
  'PRODUCTS.SEARCH_PLACEHOLDER': 'ابحث بالاسم أو الاسم العلمي أو الشركة…',
  'PRODUCTS.NO_MATCHES': 'لا توجد نتائج مطابقة.',
  'PRODUCTS.SELECTED': 'تم الاختيار من القاعدة — عدّل قبل الحفظ',
  'PRODUCTS.EDIT_BEFORE_SAVE': 'راجع وعدّل بيانات المنتج قبل الحفظ.',
  'PRODUCTS.CODE': 'الكود',
  'PRODUCTS.NAME_EN': 'الاسم (إنجليزي)',
  'PRODUCTS.NAME_AR': 'الاسم (عربي)',
  'PRODUCTS.BARCODE': 'الباركود',
  'PRODUCTS.PRICE': 'السعر'
};
