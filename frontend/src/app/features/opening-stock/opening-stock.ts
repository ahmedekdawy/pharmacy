import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom, timeout } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { SyncService } from '../../core/offline/sync.service';
import { PosProduct, formatStock, newId, round2, unitsPerBox } from '../../core/offline/models';
import { barcodeCandidates, normalizeBarcode, parseGs1 } from '../../core/offline/gs1';
import { escapeHtml, money, printHtml } from '../../core/offline/print';
import { BarcodeScanner } from '../../shared/ui/barcode-scanner';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }

interface CatalogDrug {
  commercialNameEn: string; commercialNameAr: string; scientificName: string; manufacturer: string; priceEgp: number;
}

interface BarcodeLookup {
  barcode: string;
  source: 'Product' | 'Registry' | 'External' | 'None';
  product?: {
    id: string; code: string; nameAr: string; nameEn: string; barcode?: string | null; sellingPrice: number;
    stripsPerBox: number; unitsPerStrip: number; allowStripSale: boolean; allowUnitSale: boolean;
    stripPrice?: number | null; unitPrice?: number | null;
  } | null;
  suggestion?: {
    nameEn: string; nameAr: string; scientificName?: string | null; manufacturer?: string | null; price?: number | null;
    stripsPerBox: number; unitsPerStrip: number; source: string; confirmations: number;
  } | null;
  catalogMatches: CatalogDrug[];
}

interface SessionLine {
  id: string;
  at: string;
  productId: string;
  name: string;
  boxes: number;
  strips: number;
  pills: number;
  batch: string;
  expiry: string | null;
  costPerBox: number;
  queued: boolean;
}

interface ReviewLine {
  id: string; productCode: string; productNameEn: string; productNameAr: string; barcode?: string | null;
  batchNumber?: string | null; expiryDate?: string | null; baseQuantity: number; unitsPerBox: number;
  costValue: number; sellingValue: number; createdAt: string;
}

type Step = 'scan' | 'new' | 'count';

const LOCATION_KEY = 'pharmacy.pos_location';
const SESSION_KEY = 'pharmacy.opening_session';
const MARGIN_KEY = 'pharmacy.opening_margin';

@Component({
  selector: 'app-opening-stock',
  standalone: true,
  imports: [ReactiveFormsModule, DecimalPipe, DatePipe, BarcodeScanner],
  templateUrl: './opening-stock.html',
  styleUrl: './opening-stock.scss'
})
export class OpeningStockPage implements OnInit {
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly sync = inject(SyncService);
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  private readonly scanBox = viewChild<ElementRef<HTMLInputElement>>('scanBox');

  readonly tab = signal<'count' | 'review'>('count');
  readonly step = signal<Step>('scan');
  readonly locationId = signal(localStorage.getItem(LOCATION_KEY) ?? '');
  readonly scanText = signal('');
  readonly cameraOpen = signal(false);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly sourceNote = signal<string | null>(null);
  readonly current = signal<PosProduct | null>(null);
  readonly catalogResults = signal<CatalogDrug[]>([]);
  readonly session = signal<SessionLine[]>(readSession());
  readonly margin = signal<number | null>(readMargin());
  readonly review = signal<ReviewLine[] | null>(null);

  readonly lang = computed(() => this.i18n.language());
  readonly canCreateProduct = computed(() => this.auth.hasPermission('Product.Create'));

  /** Name/code search when the typed text is not a barcode. */
  readonly nameResults = computed(() => {
    const term = this.scanText().trim().toLowerCase();
    if (term.length < 2 || /^\d{6,}$/.test(term)) return [];
    return this.sync
      .products()
      .filter((p) => p.nameEn.toLowerCase().includes(term) || p.nameAr.includes(term) || p.code.toLowerCase().includes(term))
      .slice(0, 12);
  });

  readonly sessionTotals = computed(() => {
    const lines = this.session();
    return {
      lines: lines.length,
      cost: round2(lines.reduce((s, l) => s + this.lineBoxes(l) * l.costPerBox, 0))
    };
  });

  readonly reviewTotals = computed(() => {
    const r = this.review() ?? [];
    return {
      cost: round2(r.reduce((s, x) => s + x.costValue, 0)),
      selling: round2(r.reduce((s, x) => s + x.sellingValue, 0)),
      items: new Set(r.map((x) => x.productCode)).size
    };
  });

  readonly productForm = this.fb.nonNullable.group({
    id: [''],
    barcode: [''],
    code: ['', Validators.required],
    nameAr: ['', Validators.required],
    nameEn: ['', Validators.required],
    sellingPrice: [0, [Validators.required, Validators.min(0)]],
    stripsPerBox: [1, [Validators.required, Validators.min(1)]],
    unitsPerStrip: [1, [Validators.required, Validators.min(1)]],
    allowStripSale: [true],
    allowUnitSale: [false],
    scientificName: [''],
    manufacturer: ['']
  });

  readonly countForm = this.fb.nonNullable.group({
    boxes: [0, [Validators.min(0)]],
    strips: [0, [Validators.min(0)]],
    pills: [0, [Validators.min(0)]],
    batch: ['', Validators.required],
    expiry: [''],
    costPerBox: [0, [Validators.min(0)]]
  });

  ngOnInit(): void {
    void this.sync.init().then(() => {
      const locations = this.sync.locations();
      if (!locations.some((l) => l.id === this.locationId()) && locations.length) this.setLocation(locations[0].id);
    });
  }

  // ---------------------------------------------------------------- template helpers

  pick(en: string, ar: string): string {
    return this.i18n.pick(en, ar);
  }

  name(p: { nameEn: string; nameAr: string }): string {
    return this.lang() === 'ar' ? p.nameAr || p.nameEn : p.nameEn || p.nameAr;
  }

  stockText(p: PosProduct): string {
    return formatStock(p, this.lang());
  }

  cameraSupported(): boolean {
    return BarcodeScanner.isSupported();
  }

  lineBoxes(l: SessionLine): number {
    const p = this.sync.products().find((x) => x.id === l.productId);
    const per = p ? unitsPerBox(p) : 1;
    const perStrip = p ? Math.max(1, p.unitsPerStrip) : 1;
    return (l.boxes * per + l.strips * perStrip + l.pills) / per;
  }

  countSummary(): string {
    const p = this.current();
    if (!p) return '';
    const v = this.countForm.getRawValue();
    const total = this.totalPills(p);
    return `${total} ${this.pick('pills', 'حبة')} = ${round2(total / unitsPerBox(p))} ${this.pick('boxes', 'علبة')}` +
      (v.costPerBox ? ` · ${this.pick('cost', 'تكلفة')} ${money((total / unitsPerBox(p)) * Number(v.costPerBox))}` : '');
  }

  // ---------------------------------------------------------------- actions

  setLocation(id: string): void {
    this.locationId.set(id);
    localStorage.setItem(LOCATION_KEY, id);
  }

  setMargin(value: string): void {
    const n = value === '' ? null : Math.min(90, Math.max(0, Number(value)));
    this.margin.set(n);
    if (n === null) localStorage.removeItem(MARGIN_KEY);
    else localStorage.setItem(MARGIN_KEY, String(n));
  }

  onCameraScan(raw: string): void {
    this.cameraOpen.set(false);
    void this.lookup(raw);
  }

  onScanEnter(): void {
    const text = this.scanText().trim();
    if (!text) return;
    if (this.nameResults().length === 1 && !/^\d{6,}$/.test(text) && !text.startsWith('01')) {
      this.choose(this.nameResults()[0]);
      return;
    }
    void this.lookup(text);
  }

  choose(p: PosProduct): void {
    this.startCount(p, null, null);
    this.sourceNote.set(this.pick('Found in your products', 'موجود في أصنافك'));
  }

  async lookup(raw: string): Promise<void> {
    this.error.set(null);
    this.message.set(null);
    this.sourceNote.set(null);
    const gs1 = parseGs1(raw);
    const barcode = normalizeBarcode(raw);
    const candidates = barcodeCandidates(raw);

    // 1) this device's catalog (works offline)
    const local = this.sync.products().find((p) => p.barcode && candidates.includes(normalizeBarcode(p.barcode)));
    if (local) {
      this.sourceNote.set(this.pick('Found in your products', 'موجود في أصنافك'));
      this.startCount(local, gs1.batch, gs1.expiry);
      return;
    }

    // 2) server: your products, shared registry, external API
    let result: BarcodeLookup | null = null;
    if (this.sync.online()) {
      this.busy.set(true);
      try {
        const res = await firstValueFrom(
          this.http
            .get<ApiResponse<BarcodeLookup>>(`${environment.apiBaseUrl}/catalog/barcode`, { params: { code: raw } })
            .pipe(timeout(10_000))
        );
        result = res.data;
      } catch {
        /* offline or server error – continue as a new product */
      } finally {
        this.busy.set(false);
      }
    }

    if (result?.product) {
      const p = result.product;
      this.sourceNote.set(this.pick('Found in your products', 'موجود في أصنافك'));
      this.startCount({ ...p, stock: 0 } as PosProduct, gs1.batch, gs1.expiry);
      return;
    }

    if (!this.canCreateProduct()) {
      this.error.set(this.pick('Item not found and you are not allowed to add products.', 'الصنف غير موجود وليس لديك صلاحية إضافة أصناف.'));
      return;
    }

    this.startNewProduct(barcode, gs1.batch, gs1.expiry, result);
  }

  pickCatalog(d: CatalogDrug): void {
    this.productForm.patchValue({
      nameEn: d.commercialNameEn || d.scientificName,
      nameAr: d.commercialNameAr || d.commercialNameEn,
      sellingPrice: Number(d.priceEgp) || this.productForm.getRawValue().sellingPrice,
      scientificName: d.scientificName,
      manufacturer: d.manufacturer
    });
    this.catalogResults.set([]);
  }

  searchCatalog(term: string): void {
    if (term.trim().length < 2 || !this.sync.online()) {
      this.catalogResults.set([]);
      return;
    }
    this.http
      .get<ApiResponse<CatalogDrug[]>>(`${environment.apiBaseUrl}/catalog/egyptian-drugs`, { params: { search: term.trim(), limit: 10 } })
      .subscribe({ next: (r) => this.catalogResults.set(r.data ?? []), error: () => this.catalogResults.set([]) });
  }

  async saveProduct(): Promise<void> {
    if (this.productForm.invalid) {
      this.productForm.markAllAsTouched();
      return;
    }
    const v = this.productForm.getRawValue();
    const strips = Math.max(1, Math.floor(Number(v.stripsPerBox)));
    const units = Math.max(1, Math.floor(Number(v.unitsPerStrip)));
    const payload = {
      id: v.id || newId(),
      code: v.code.trim(),
      barcode: v.barcode.trim() || null,
      nameAr: v.nameAr.trim(),
      nameEn: v.nameEn.trim(),
      sellingPrice: Number(v.sellingPrice) || 0,
      stripsPerBox: strips,
      unitsPerStrip: units,
      allowStripSale: v.allowStripSale && strips > 1,
      allowUnitSale: v.allowUnitSale && strips * units > 1,
      scientificName: v.scientificName.trim() || null,
      manufacturer: v.manufacturer.trim() || null
    };

    this.busy.set(true);
    try {
      const outcome = await this.sync.submit('product.create', payload, `${this.pick('New product', 'صنف جديد')}: ${payload.nameAr}`);
      if (outcome.kind === 'rejected') {
        this.error.set(outcome.error);
        return;
      }
      const created = this.sync.products().find((p) => p.id === payload.id);
      const batch = this.countForm.getRawValue().batch;
      const expiry = this.countForm.getRawValue().expiry;
      if (created) this.startCount(created, batch, expiry || null);
      this.sourceNote.set(
        outcome.kind === 'queued'
          ? this.pick('Product saved on this device – it will upload automatically.', 'تم حفظ الصنف على الجهاز وسيُرفع تلقائياً.')
          : this.pick('Product added', 'تمت إضافة الصنف')
      );
    } finally {
      this.busy.set(false);
    }
  }

  async saveCount(): Promise<void> {
    const p = this.current();
    const location = this.locationId();
    if (!p) return;
    if (!location) {
      this.error.set(this.pick('Choose the branch first.', 'اختر الفرع أولاً.'));
      return;
    }
    const v = this.countForm.getRawValue();
    const total = this.totalPills(p);
    if (!(total > 0)) {
      this.error.set(this.pick('Enter the counted quantity.', 'أدخل الكمية المعدودة.'));
      return;
    }
    if (this.countForm.invalid) {
      this.countForm.markAllAsTouched();
      return;
    }

    const id = newId();
    const per = unitsPerBox(p);
    const payload = {
      id,
      locationId: location,
      productId: p.id,
      batchNumber: v.batch.trim(),
      expiryDate: v.expiry || null,
      purchasePrice: (Number(v.costPerBox) || 0) / per, // cost per pill
      quantity: total, // in pills
      unit: 3
    };

    this.busy.set(true);
    try {
      const outcome = await this.sync.submit('openingBalance.create', payload, `${this.pick('Opening stock', 'رصيد أول المدة')}: ${this.name(p)} × ${round2(total / per)}`);
      if (outcome.kind === 'rejected') {
        this.error.set(outcome.error);
        return;
      }
      const line: SessionLine = {
        id, at: new Date().toISOString(), productId: p.id, name: this.name(p),
        boxes: Number(v.boxes) || 0, strips: Number(v.strips) || 0, pills: Number(v.pills) || 0,
        batch: payload.batchNumber, expiry: payload.expiryDate, costPerBox: Number(v.costPerBox) || 0,
        queued: outcome.kind === 'queued'
      };
      this.session.update((s) => [line, ...s]);
      persistSession(this.session());
      this.message.set(`✓ ${this.name(p)} — ${round2(total / per)} ${this.pick('boxes', 'علبة')}`);
      this.reset();
    } finally {
      this.busy.set(false);
    }
  }

  cancel(): void {
    this.reset();
  }

  clearSession(): void {
    if (!confirm(this.pick('Clear this list? (Saved stock is not affected.)', 'مسح القائمة؟ (الرصيد المحفوظ لن يتأثر)'))) return;
    this.session.set([]);
    persistSession([]);
  }

  loadReview(): void {
    this.tab.set('review');
    this.review.set(null);
    this.error.set(null);
    const params: Record<string, string> = this.locationId() ? { locationId: this.locationId() } : {};
    this.http.get<ApiResponse<ReviewLine[]>>(`${environment.apiBaseUrl}/inventory/opening-balance`, { params }).subscribe({
      next: (r) => this.review.set(r.data ?? []),
      error: () => this.error.set(this.pick('Review needs an internet connection.', 'المراجعة تحتاج اتصال بالإنترنت.'))
    });
  }

  printReview(): void {
    const rows = this.review() ?? [];
    const ar = this.lang() === 'ar';
    const L = (en: string, a: string) => this.pick(en, a);
    const body = rows
      .map((r) => `<tr><td>${escapeHtml(r.productCode)}</td><td>${escapeHtml(ar ? r.productNameAr : r.productNameEn)}</td>
        <td>${escapeHtml(r.batchNumber ?? '')}</td><td>${escapeHtml(r.expiryDate ?? '')}</td>
        <td class="num">${round2(r.baseQuantity / r.unitsPerBox)}</td><td class="num">${money(r.costValue)}</td><td class="num">${money(r.sellingValue)}</td></tr>`)
      .join('');
    const t = this.reviewTotals();
    printHtml(
      `<h1>${L('Opening stock count', 'حصر رصيد أول المدة')}</h1>
       <div class="center">${escapeHtml(this.auth.user()?.tenantCode ?? '')} — ${new Date().toLocaleDateString(ar ? 'ar-EG' : 'en-GB')}</div><hr>
       <table><thead><tr><th>${L('Code', 'الكود')}</th><th>${L('Item', 'الصنف')}</th><th>${L('Batch', 'التشغيلة')}</th><th>${L('Expiry', 'الصلاحية')}</th>
       <th class="num">${L('Boxes', 'علب')}</th><th class="num">${L('Cost', 'التكلفة')}</th><th class="num">${L('Sale value', 'قيمة البيع')}</th></tr></thead>
       <tbody>${body}</tbody></table><hr>
       <div class="grid"><div>${L('Items', 'عدد الأصناف')}: <b>${t.items}</b></div><div>${L('Lines', 'السطور')}: <b>${rows.length}</b></div>
       <div>${L('Total cost', 'إجمالي التكلفة')}: <b>${money(t.cost)}</b></div><div>${L('Total sale value', 'إجمالي قيمة البيع')}: <b>${money(t.selling)}</b></div></div>`,
      { title: 'opening-stock', dir: ar ? 'rtl' : 'ltr', paperWidth: 'A4' }
    );
  }

  // ---------------------------------------------------------------- internals

  private startCount(p: PosProduct, batch: string | null, expiry: string | null): void {
    this.current.set(p);
    const margin = this.margin();
    this.countForm.reset({
      boxes: 0,
      strips: 0,
      pills: 0,
      batch: batch || 'OPENING',
      expiry: expiry ?? '',
      costPerBox: margin !== null ? round2(p.sellingPrice * (1 - margin / 100)) : 0
    });
    this.step.set('count');
    this.scanText.set('');
  }

  private startNewProduct(barcode: string, batch: string | null, expiry: string | null, result: BarcodeLookup | null): void {
    const s = result?.suggestion;
    const catalogPrice = result?.catalogMatches?.[0]?.priceEgp;
    this.productForm.reset({
      id: newId(),
      barcode,
      code: barcode || `P-${Date.now().toString(36).toUpperCase()}`,
      nameAr: s?.nameAr ?? '',
      nameEn: s?.nameEn ?? '',
      sellingPrice: Number(s?.price ?? catalogPrice ?? 0),
      stripsPerBox: s?.stripsPerBox ?? 1,
      unitsPerStrip: s?.unitsPerStrip ?? 1,
      allowStripSale: true,
      allowUnitSale: false,
      scientificName: s?.scientificName ?? '',
      manufacturer: s?.manufacturer ?? ''
    });
    this.catalogResults.set(s ? result?.catalogMatches ?? [] : []);
    this.countForm.patchValue({ batch: batch || 'OPENING', expiry: expiry ?? '' });
    this.sourceNote.set(
      s
        ? this.pick(
            `Data loaded from ${s.source === 'External' ? 'the drug API' : 'the shared barcode registry'} – please review.`,
            `تم تحميل البيانات من ${s.source === 'External' ? 'خدمة الأدوية' : 'سجل الباركود المشترك'} – راجعها قبل الحفظ.`
          )
        : this.pick('New item – enter its data (or search the Egyptian drug list).', 'صنف جديد – أدخل بياناته (أو ابحث في قائمة الأدوية المصرية).')
    );
    this.step.set('new');
    this.scanText.set('');
  }

  private totalPills(p: PosProduct): number {
    const v = this.countForm.getRawValue();
    return (Number(v.boxes) || 0) * unitsPerBox(p) + (Number(v.strips) || 0) * Math.max(1, p.unitsPerStrip) + (Number(v.pills) || 0);
  }

  private reset(): void {
    this.current.set(null);
    this.step.set('scan');
    this.catalogResults.set([]);
    setTimeout(() => this.scanBox()?.nativeElement.focus(), 0);
  }
}

function readSession(): SessionLine[] {
  try {
    return JSON.parse(localStorage.getItem(SESSION_KEY) ?? '[]') as SessionLine[];
  } catch {
    return [];
  }
}

function persistSession(lines: SessionLine[]): void {
  try {
    localStorage.setItem(SESSION_KEY, JSON.stringify(lines.slice(0, 500)));
  } catch {
    /* storage full – list is only a convenience */
  }
}

function readMargin(): number | null {
  const v = localStorage.getItem(MARGIN_KEY);
  return v === null ? null : Number(v);
}
