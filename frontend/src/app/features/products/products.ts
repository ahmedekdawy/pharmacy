import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, Subscription, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, ApiService, ProductDto } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { IconButton } from '../../shared/ui/icon-button';
import { apiErrorMessage } from '../../core/http-error';

export interface EgyptianDrug {
  commercialNameEn: string;
  commercialNameAr: string;
  scientificName: string;
  manufacturer: string;
  drugClass: string;
  route: string;
  priceEgp: number;
}

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [ReactiveFormsModule, DecimalPipe, IconButton],
  templateUrl: './products.html',
  styleUrl: './products.scss'
})
export class Products implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  private readonly search$ = new Subject<string>();
  private searchSub?: Subscription;
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);

  readonly products = signal<ProductDto[]>([]);
  readonly catalogResults = signal<EgyptianDrug[]>([]);
  readonly selectedDrug = signal<EgyptianDrug | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly catalogLoading = signal(false);
  readonly catalogOpen = signal(false);
  readonly codeError = signal<string | null>(null);
  readonly packGuess = signal<string | null>(null);

  readonly catalogSearch = this.fb.nonNullable.control('');
  readonly form = this.fb.group({
    code: ['', Validators.required],
    nameAr: ['', Validators.required],
    nameEn: ['', Validators.required],
    barcode: [''],
    sellingPrice: [0, [Validators.required, Validators.min(0)]],
    isActive: [true],
    stripsPerBox: [1, [Validators.required, Validators.min(1)]],
    unitsPerStrip: [1, [Validators.required, Validators.min(1)]],
    allowStripSale: [false],
    allowUnitSale: [false],
    stripPrice: [null as number | null],
    unitPrice: [null as number | null]
  });

  derivedStripPrice(): string {
    const v = this.form.getRawValue();
    return (Number(v.sellingPrice) / Math.max(1, Number(v.stripsPerBox) || 1)).toFixed(2);
  }

  derivedUnitPrice(): string {
    const v = this.form.getRawValue();
    const units = Math.max(1, Number(v.stripsPerBox) || 1) * Math.max(1, Number(v.unitsPerStrip) || 1);
    return (Number(v.sellingPrice) / units).toFixed(2);
  }

  ngOnInit(): void {
    this.reload();
    this.searchSub = this.search$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => {
          this.catalogLoading.set(true);
          return this.http.get<ApiResponse<EgyptianDrug[]>>(
            `${environment.apiBaseUrl}/catalog/egyptian-drugs`,
            { params: { search: term, limit: 20 } }
          );
        })
      )
      .subscribe({
        next: (res) => {
          this.catalogResults.set(res.data ?? []);
          this.catalogOpen.set(true);
          this.catalogLoading.set(false);
        },
        error: () => {
          this.catalogLoading.set(false);
          this.error.set(this.i18n.pick('Unable to search Egyptian drug catalog.', 'تعذر البحث في دليل الأدوية المصرية.'));
        }
      });
  }

  ngOnDestroy(): void {
    this.searchSub?.unsubscribe();
  }

  t(key: string): string {
    return this.i18n.translate(key);
  }

  onCatalogInput(value: string): void {
    this.catalogSearch.setValue(value);
    if (!value.trim()) {
      this.catalogResults.set([]);
      this.catalogOpen.set(false);
      return;
    }
    this.search$.next(value.trim());
  }

  selectDrug(drug: EgyptianDrug): void {
    this.editingId.set(null);
    this.selectedDrug.set(drug);
    this.catalogOpen.set(false);
    this.catalogSearch.setValue(drug.commercialNameEn || drug.commercialNameAr);
    this.codeError.set(null);
    const pack = guessPack(drug);
    this.form.patchValue({
      code: uniqueCode(buildProductCode(drug), this.products().map((p) => p.code)),
      nameEn: drug.commercialNameEn || drug.scientificName,
      nameAr: drug.commercialNameAr || drug.commercialNameEn,
      barcode: '',
      sellingPrice: Number(drug.priceEgp) || 0,
      isActive: true,
      stripsPerBox: pack?.strips ?? 1,
      unitsPerStrip: pack?.perStrip ?? 1
    });
    this.packGuess.set(
      pack
        ? this.i18n.pick(
            `Pack filled from the name: ${pack.total} units = ${pack.strips} × ${pack.perStrip}. Check it against the box.`,
            `تم ملء العبوة من الاسم: ${pack.total} وحدة = ${pack.strips} × ${pack.perStrip}. راجعها على العلبة.`
          )
        : null
    );
    this.form.markAsDirty();
  }

  startEdit(product: ProductDto): void {
    this.selectedDrug.set(null);
    this.catalogOpen.set(false);
    this.editingId.set(product.id);
    this.form.patchValue({
      code: product.code,
      nameEn: product.nameEn,
      nameAr: product.nameAr,
      barcode: product.barcode ?? '',
      sellingPrice: product.sellingPrice,
      isActive: product.isActive,
      stripsPerBox: product.stripsPerBox ?? 1,
      unitsPerStrip: product.unitsPerStrip ?? 1,
      allowStripSale: !!product.allowStripSale,
      allowUnitSale: !!product.allowUnitSale,
      stripPrice: product.stripPrice ?? null,
      unitPrice: product.unitPrice ?? null
    });
  }

  cancelEdit(): void {
    this.clearForm();
  }

  clearForm(): void {
    this.editingId.set(null);
    this.selectedDrug.set(null);
    this.codeError.set(null);
    this.packGuess.set(null);
    this.catalogSearch.setValue('');
    this.catalogResults.set([]);
    this.catalogOpen.set(false);
    this.form.reset({
      sellingPrice: 0,
      isActive: true,
      stripsPerBox: 1,
      unitsPerStrip: 1,
      allowStripSale: false,
      allowUnitSale: false,
      stripPrice: null,
      unitPrice: null
    });
  }

  reload(): void {
    this.loading.set(true);
    this.api.searchProducts('', 1, 100).subscribe({
      next: (res) => {
        this.products.set(res.data?.items ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(this.i18n.pick(
          'Unable to load products. Ensure API is running and tenant header is set.',
          'تعذر تحميل الأصناف. تأكد من تشغيل الخادم وتحديد كود الصيدلية.'
        ));
        this.loading.set(false);
      }
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const editingId = this.editingId();
    const code = (value.code ?? '').trim().toUpperCase();
    if (this.products().some((p) => p.id !== editingId && p.code.toUpperCase() === code)) {
      this.codeError.set(this.i18n.pick('This code is already used by another product.', 'هذا الكود مستخدم لصنف آخر.'));
      return;
    }
    this.error.set(null);
    const payload = {
      code: value.code!,
      nameAr: value.nameAr!,
      nameEn: value.nameEn!,
      barcode: value.barcode || undefined,
      sellingPrice: Number(value.sellingPrice),
      isActive: !!value.isActive,
      stripsPerBox: Math.max(1, Math.floor(Number(value.stripsPerBox) || 1)),
      unitsPerStrip: Math.max(1, Math.floor(Number(value.unitsPerStrip) || 1)),
      allowStripSale: !!value.allowStripSale,
      allowUnitSale: !!value.allowUnitSale,
      stripPrice: value.stripPrice === null || value.stripPrice === undefined || `${value.stripPrice}` === '' ? null : Number(value.stripPrice),
      unitPrice: value.unitPrice === null || value.unitPrice === undefined || `${value.unitPrice}` === '' ? null : Number(value.unitPrice)
    };

    if (editingId) {
      this.api.updateProduct(editingId, payload).subscribe({
        next: () => {
          this.clearForm();
          this.reload();
        },
        error: (err) => this.showSaveError(apiErrorMessage(err, this.i18n.pick('Update product failed.', 'فشل تحديث الصنف.')))
      });
      return;
    }

    this.api.createProduct(payload).subscribe({
      next: () => {
        this.clearForm();
        this.reload();
      },
      error: (err) => this.showSaveError(apiErrorMessage(err, this.i18n.pick('Create product failed.', 'فشل إضافة الصنف.')))
    });
  }

  /** Code conflicts are shown under the code field; anything else at the top of the page. */
  private showSaveError(message: string): void {
    if (/product code|كود الصنف/i.test(message)) {
      this.codeError.set(message);
      return;
    }
    this.error.set(message);
  }

  remove(product: ProductDto): void {
    if (!confirm(this.i18n.pick(`Delete product ${product.code}?`, `حذف الصنف ${product.code}؟`))) {
      return;
    }

    this.api.deleteProduct(product.id).subscribe({
      next: () => {
        if (this.editingId() === product.id) {
          this.clearForm();
        }
        this.reload();
      },
      error: (err) => this.error.set(apiErrorMessage(err, this.i18n.pick('Delete product failed.', 'فشل حذف الصنف.')))
    });
  }
}

/**
 * Code from the whole commercial name (pack size included) so sizes of the same drug get different codes:
 * "Panadol Advance 500mg 24 tab" → EG-PANADOL-ADVANCE-500MG-24-TAB. Backend limit is 64 chars.
 */
export function buildProductCode(drug: EgyptianDrug): string {
  const source = (drug.commercialNameEn || drug.scientificName || 'DRUG').toUpperCase();
  const slug = source.replace(/[^A-Z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 56).replace(/-+$/, '') || 'DRUG';
  return `EG-${slug}`;
}

/** Appends -2, -3… when the code is already taken. */
export function uniqueCode(code: string, existing: string[]): string {
  const taken = new Set(existing.map((c) => c.toUpperCase()));
  if (!taken.has(code.toUpperCase())) return code;
  for (let i = 2; ; i++) {
    const candidate = `${code}-${i}`;
    if (!taken.has(candidate.toUpperCase())) return candidate;
  }
}

/**
 * Reads the pack size from the drug name ("24 f.c. tabs", "30 caps", "علبة 20 قرص").
 * Blister strips are usually 10 units, so 20/30 → 2/3 strips of 10; other counts stay as one strip.
 */
export function guessPack(drug: EgyptianDrug): { total: number; strips: number; perStrip: number } | null {
  const text = `${drug.commercialNameEn} ${drug.commercialNameAr}`;
  const match =
    /(\d{1,4})\s*(?:f\.?\s*c\.?\s*)?(?:tabs?|tablets?|caps?|capsules?|cap\.|sachets?|supp)\b/i.exec(text) ??
    /(\d{1,4})\s*(?:قرص|أقراص|اقراص|كبسول|كبسولة|كيس|لبوس)/.exec(text);
  const total = match ? Number(match[1]) : 0;
  if (!total || total > 1000) return null;
  const strips = total > 10 && total % 10 === 0 ? total / 10 : 1;
  return { total, strips, perStrip: total / strips };
}
