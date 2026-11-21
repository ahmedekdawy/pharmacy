import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, Subscription, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { ApiService, EgyptianDrug, ProductDto } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { SyncService } from '../../core/offline/sync.service';
import { PosProduct } from '../../core/offline/models';
import { buildProductCode, guessPack, uniqueCode } from '../../core/catalog/drug-product-mapping';
import { IconButton } from '../../shared/ui/icon-button';
import { BarcodeScanner } from '../../shared/ui/barcode-scanner';
import { apiErrorMessage } from '../../core/http-error';

export type { EgyptianDrug };

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [ReactiveFormsModule, DecimalPipe, IconButton, BarcodeScanner],
  templateUrl: './products.html',
  styleUrl: './products.scss'
})
export class Products implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  private readonly search$ = new Subject<string>();
  private searchSub?: Subscription;
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly sync = inject(SyncService);

  readonly products = signal<ProductDto[]>([]);
  readonly listSearch = signal('');
  readonly catalogResults = signal<EgyptianDrug[]>([]);
  readonly selectedDrug = signal<EgyptianDrug | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly offlineNotice = signal<string | null>(null);
  readonly loading = signal(false);
  readonly catalogLoading = signal(false);
  readonly catalogOpen = signal(false);
  readonly codeError = signal<string | null>(null);
  readonly packGuess = signal<string | null>(null);
  readonly cameraOpen = signal(false);

  /** Filters the already-loaded list client-side, so this keeps working offline too. */
  readonly filteredProducts = computed(() => {
    const term = this.listSearch().trim().toLowerCase();
    if (!term) return this.products();
    return this.products().filter((p) =>
      `${p.code} ${p.nameEn} ${p.nameAr} ${p.barcode ?? ''}`.toLowerCase().includes(term)
    );
  });

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
    // Loads the local (IndexedDB) product snapshot first, so reload() has something to fall
    // back to immediately if the pharmacy is offline.
    void this.sync.init().then(() => this.reload());
    this.searchSub = this.search$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => {
          this.catalogLoading.set(true);
          // This external drug registry has no offline copy; catchError turns a failed
          // request into a normal (null) result instead of killing this subscription —
          // otherwise one failed search (e.g. offline) would silently break every search
          // after it, even once back online.
          return this.api
            .searchEgyptianDrugs(term, 20)
            .pipe(catchError((err) => of({ failed: true as const, err })));
        })
      )
      .subscribe((res) => {
        this.catalogLoading.set(false);
        if ('failed' in res) {
          this.catalogResults.set([]);
          if (!(res.err instanceof HttpErrorResponse) || res.err.status !== 0) {
            this.error.set(this.i18n.pick('Unable to search Egyptian drug catalog.', 'تعذر البحث في دليل الأدوية المصرية.'));
          }
          return;
        }
        this.catalogResults.set(res.data ?? []);
        this.catalogOpen.set(true);
      });
  }

  ngOnDestroy(): void {
    this.searchSub?.unsubscribe();
  }

  t(key: string): string {
    return this.i18n.translate(key);
  }

  cameraSupported(): boolean {
    return BarcodeScanner.isSupported();
  }

  onCameraScan(raw: string): void {
    this.cameraOpen.set(false);
    this.form.patchValue({ barcode: raw });
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
    this.offlineNotice.set(null);
    this.api.searchProducts('', 1, 100).subscribe({
      next: (res) => {
        this.products.set(res.data?.items ?? []);
        this.loading.set(false);
      },
      error: (err) => {
        if (this.showOfflineCache(err)) return;
        this.error.set(this.i18n.pick(
          'Unable to load products. Ensure API is running and tenant header is set.',
          'تعذر تحميل الأصناف. تأكد من تشغيل الخادم وتحديد كود الصيدلية.'
        ));
        this.loading.set(false);
      }
    });
  }

  /**
   * When the backend can't be reached, fall back to the product snapshot the device already
   * downloaded for the POS (kept in IndexedDB by {@link SyncService}), so this list — and
   * searching it — keeps working offline. Returns false (does nothing) for a real server error.
   */
  private showOfflineCache(err: unknown): boolean {
    const isNetworkError = err instanceof HttpErrorResponse && (err.status === 0 || err.status === 408);
    const cached = this.sync.products();
    if (!isNetworkError || !cached.length) return false;

    this.products.set(cached.map(posProductToDto));
    this.offlineNotice.set(this.i18n.pick(
      'Offline — showing the catalog last downloaded for the POS.',
      'غير متصل بالإنترنت — يعرض آخر نسخة من الأصناف تم تحميلها لنقطة البيع.'
    ));
    this.loading.set(false);
    return true;
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
 * The POS snapshot has no `isActive`/`categoryId` (it only ever holds sellable products), so
 * those are filled with reasonable defaults when it's reused here as an offline fallback.
 */
function posProductToDto(p: PosProduct): ProductDto {
  return {
    id: p.id,
    code: p.code,
    nameAr: p.nameAr,
    nameEn: p.nameEn,
    barcode: p.barcode ?? null,
    categoryId: null,
    sellingPrice: p.sellingPrice,
    isActive: true,
    stripsPerBox: p.stripsPerBox,
    unitsPerStrip: p.unitsPerStrip,
    allowStripSale: p.allowStripSale,
    allowUnitSale: p.allowUnitSale,
    stripPrice: p.stripPrice ?? null,
    unitPrice: p.unitPrice ?? null
  };
}
