import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, computed, effect, inject, signal, viewChild } from '@angular/core';
import { Subject, Subscription, catchError, debounceTime, map, of, switchMap } from 'rxjs';
import { ApiService, EgyptianDrug } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { SyncService } from '../../core/offline/sync.service';
import { EgyptianDrugCatalogService } from '../../core/catalog/egyptian-drug-catalog.service';
import { buildProductCode, guessPack, uniqueCode } from '../../core/catalog/drug-product-mapping';
import { apiErrorMessage } from '../../core/http-error';
import {
  LocalReceipt,
  PaymentMethod,
  PosCustomer,
  PosProduct,
  SaleUnit,
  allowedUnits,
  formatStock,
  newId,
  round2,
  unitFactor,
  unitLabel,
  unitPrice
} from '../../core/offline/models';
import { printHtml, receiptHtml } from '../../core/offline/print';
import { barcodeCandidates, normalizeBarcode } from '../../core/offline/gs1';
import { BarcodeScanner } from '../../shared/ui/barcode-scanner';
import { ShortageQuickAdd } from '../shortages/shortage-quick-add';

interface CartLine {
  key: string;
  product: PosProduct;
  unit: SaleUnit;
  qty: number;
  price: number;
}

const LOCATION_KEY = 'pharmacy.pos_location';

@Component({
  selector: 'app-pos',
  standalone: true,
  imports: [DecimalPipe, NgTemplateOutlet, BarcodeScanner, ShortageQuickAdd],
  templateUrl: './pos.html',
  styleUrl: './pos.scss'
})
export class PosPage implements OnInit, AfterViewInit, OnDestroy {
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly sync = inject(SyncService);
  private readonly api = inject(ApiService);
  private readonly egyptianDrugs = inject(EgyptianDrugCatalogService);
  private readonly searchBox = viewChild<ElementRef<HTMLInputElement>>('searchBox');

  readonly Unit = SaleUnit;
  readonly query = signal('');
  readonly cart = signal<CartLine[]>([]);
  readonly locationId = signal<string>(localStorage.getItem(LOCATION_KEY) ?? '');
  readonly customerId = signal<string>('');
  readonly customerQuery = signal('');
  readonly discount = signal(0);
  readonly paymentMethod = signal<PaymentMethod>('Cash');
  readonly paidInput = signal<number | null>(null);
  readonly notes = signal('');
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);
  readonly receipt = signal<LocalReceipt | null>(null);
  readonly showNewCustomer = signal(false);
  readonly newCustomerName = signal('');
  readonly newCustomerPhone = signal('');
  /** Shortage dialog: open with a catalog product, or with free text (item not in the catalog). */
  readonly shortageDialog = signal<{ product: PosProduct | null; name: string } | null>(null);
  readonly toast = signal<string | null>(null);
  private toastTimer?: ReturnType<typeof setTimeout>;

  /** Set when the one exact search match's catalog price differs from what's charged here. */
  readonly priceCheck = signal<{ product: PosProduct; latest: EgyptianDrug } | null>(null);
  readonly priceCheckBusy = signal(false);
  private readonly priceCheckRequest$ = new Subject<PosProduct>();
  private readonly priceCheckSub: Subscription;

  /** commercialNameEn of the drug currently being added, if any (there's no stable catalog id). */
  readonly addingDrugKey = signal<string | null>(null);

  /**
   * Whichever cart line or search result is showing its "alternatives" panel right now (one at
   * a time) — a cart line swaps in the chosen alternative, a search result just adds it to the
   * cart as a new line, same as clicking the result itself would.
   */
  readonly alternativesContext = signal<{ kind: 'cart'; line: CartLine } | { kind: 'search'; product: PosProduct } | null>(null);
  readonly alternatives = computed(() => {
    const context = this.alternativesContext();
    if (!context) return [];
    const product = context.kind === 'cart' ? context.line.product : context.product;
    return findAlternatives(product, this.sync.products(), this.egyptianDrugs.drugs());
  });

  readonly lang = computed(() => this.i18n.language());

  constructor() {
    // Only ever checks the single exact match, not every row of a broad search — this is an
    // external, best-effort reference list, so one confident comparison beats several noisy ones.
    this.priceCheckSub = this.priceCheckRequest$
      .pipe(
        debounceTime(400),
        switchMap((product) =>
          this.api.searchEgyptianDrugs(product.nameEn || product.nameAr, 5).pipe(
            map((res) => ({ product, latest: pickCatalogMatch(product, res.data ?? []) })),
            catchError(() => of(null))
          )
        )
      )
      .subscribe((result) => {
        if (!result?.latest || round2(result.latest.priceEgp) === round2(result.product.sellingPrice)) {
          this.priceCheck.set(null);
          return;
        }
        this.priceCheck.set({ product: result.product, latest: result.latest });
      });

    effect(() => {
      const matches = this.results();
      if (!this.sync.online() || matches.length !== 1 || !this.auth.hasPermission('Product.Edit')) {
        this.priceCheck.set(null);
        return;
      }
      this.priceCheckRequest$.next(matches[0]);
    });
  }

  readonly results = computed(() => {
    const term = this.query().trim().toLowerCase();
    const all = this.sync.products();
    if (!term) return [];
    // Every word must match (any order): "panadol advance 48" finds "Panadol Advance 500mg 48".
    const words = term.split(/\s+/);
    return all
      .filter((p) => {
        if ((p.barcode ?? '').toLowerCase() === term) return true;
        const text = `${p.code} ${p.nameEn} ${p.nameAr}`.toLowerCase();
        return words.every((w) => text.includes(w));
      })
      .slice(0, 30);
  });

  /** Nothing in the pharmacy's own catalog matched — offer what the drug reference list has. */
  readonly catalogSuggestions = computed(() => {
    const term = this.query().trim();
    if (!term || this.results().length) return [];
    return this.egyptianDrugs.search(term, 8);
  });

  readonly customerResults = computed(() => {
    const term = this.customerQuery().trim().toLowerCase();
    if (!term) return [];
    return this.sync
      .customers()
      .filter(
        (c) =>
          c.nameEn.toLowerCase().includes(term) ||
          c.nameAr.includes(term) ||
          c.code.toLowerCase().includes(term) ||
          (c.phone ?? '').includes(term)
      )
      .slice(0, 8);
  });

  readonly customer = computed<PosCustomer | null>(
    () => this.sync.customers().find((c) => c.id === this.customerId()) ?? null
  );

  readonly subTotal = computed(() => round2(this.cart().reduce((s, l) => s + round2(l.price * l.qty), 0)));
  readonly total = computed(() => round2(Math.max(0, this.subTotal() - Math.min(Math.max(0, this.discount() || 0), this.subTotal()))));
  readonly paid = computed(() => {
    const v = this.paidInput();
    return v === null ? this.total() : Math.min(Math.max(0, v), this.total());
  });
  readonly due = computed(() => round2(this.total() - this.paid()));
  readonly change = computed(() => {
    const v = this.paidInput();
    return v !== null && v > this.total() ? round2(v - this.total()) : 0;
  });
  readonly itemCount = computed(() => this.cart().length);

  /** Lines whose quantity exceeds what the device believes is in stock. */
  readonly shortages = computed(() => {
    const used = new Map<string, number>();
    for (const l of this.cart()) {
      used.set(l.product.id, (used.get(l.product.id) ?? 0) + l.qty * unitFactor(l.product, l.unit));
    }
    return new Set([...used.entries()].filter(([id, qty]) => qty > (this.productById(id)?.stock ?? 0)).map(([id]) => id));
  });

  readonly discountTooHigh = computed(() => (this.discount() || 0) > this.subTotal());
  readonly zeroPriceLines = computed(() => this.cart().some((l) => !(l.price > 0)));

  /** Why the sale cannot be completed right now (null = OK). Shown next to the checkout button. */
  readonly blocker = computed<string | null>(() => {
    if (!this.cart().length) return null;
    if (this.zeroPriceLines()) return this.pick('Every item needs a price greater than zero.', 'يجب أن يكون سعر كل صنف أكبر من صفر.');
    if (this.discountTooHigh()) return this.pick('Discount cannot exceed the subtotal.', 'لا يمكن أن يتجاوز الخصم إجمالي الفاتورة.');
    if (this.total() <= 0) return this.pick('The net total must be greater than zero.', 'يجب أن يكون صافي الفاتورة أكبر من صفر.');
    // Offline the local stock may be stale, and offline shortages are accepted as warnings by design.
    if (this.shortages().size && this.sync.online()) return this.pick('Some quantities exceed the available stock.', 'بعض الكميات أكبر من المتاح بالمخزن.');
    return null;
  });

  readonly creditWarning = computed(() => {
    const c = this.customer();
    if (!c || this.due() <= 0 || !c.creditLimit) return null;
    return c.balance + this.due() > c.creditLimit
      ? this.i18n.pick(
          `Credit limit ${c.creditLimit} will be exceeded (current balance ${c.balance}).`,
          `سيتم تجاوز حد الائتمان ${c.creditLimit} (الرصيد الحالي ${c.balance}).`
        )
      : null;
  });

  ngOnInit(): void {
    void this.sync.init().then(() => {
      const locations = this.sync.locations();
      if (!locations.some((l) => l.id === this.locationId()) && locations.length) {
        this.setLocation(locations[0].id);
      }
    });
    if (!this.sync.products().length) void this.sync.sync();
    void this.egyptianDrugs.init();
  }

  ngAfterViewInit(): void {
    this.focusSearch();
  }

  ngOnDestroy(): void {
    this.priceCheckSub.unsubscribe();
  }

  /**
   * Re-focusing the search box after every add keeps a barcode-scanner/physical-keyboard
   * workflow fast, but on a touch device it pops the on-screen keyboard open over the
   * product list after every single tap. Only do it when there's a fine pointer (mouse),
   * i.e. not a touch-only phone/tablet.
   */
  private focusSearch(): void {
    if (typeof matchMedia === 'function' && matchMedia('(pointer: coarse)').matches) return;
    this.searchBox()?.nativeElement.focus();
  }

  // ---------------------------------------------------------------- helpers for the template

  pick(en: string, ar: string): string {
    return this.i18n.pick(en, ar);
  }

  name(item: { nameEn: string; nameAr: string }): string {
    return this.lang() === 'ar' ? item.nameAr || item.nameEn : item.nameEn || item.nameAr;
  }

  unitName(unit: SaleUnit): string {
    return unitLabel(unit, this.lang());
  }

  units(p: PosProduct): SaleUnit[] {
    return allowedUnits(p);
  }

  stockText(p: PosProduct): string {
    return formatStock(p, this.lang());
  }

  priceOf(p: PosProduct, unit: SaleUnit): number {
    return unitPrice(p, unit);
  }

  lineTotal(l: CartLine): number {
    return round2(l.price * l.qty);
  }

  num(value: string): number {
    const n = Number(String(value).replace(',', '.'));
    return Number.isFinite(n) ? n : 0;
  }

  // ---------------------------------------------------------------- actions

  setLocation(id: string): void {
    this.locationId.set(id);
    localStorage.setItem(LOCATION_KEY, id);
  }

  readonly cameraOpen = signal(false);

  cameraSupported(): boolean {
    return BarcodeScanner.isSupported();
  }

  onCameraScan(raw: string): void {
    this.cameraOpen.set(false);
    this.query.set(raw);
    this.onSearchEnter();
  }

  dismissPriceCheck(): void {
    this.priceCheck.set(null);
  }

  applyLatestPrice(): void {
    const check = this.priceCheck();
    if (!check) return;
    const price = round2(check.latest.priceEgp);

    this.priceCheckBusy.set(true);
    this.api.updateProductPrice(check.product.id, price).subscribe({
      next: async () => {
        await this.sync.updateCachedProductPrice(check.product.id, price);
        this.priceCheckBusy.set(false);
        this.priceCheck.set(null);
        this.showToast(this.pick(`Price updated to ${price}.`, `تم تحديث السعر إلى ${price}.`));
      },
      error: (err) => {
        this.priceCheckBusy.set(false);
        this.error.set(apiErrorMessage(err, this.pick('Could not update the price.', 'تعذر تحديث السعر.')));
      }
    });
  }

  /**
   * Creates a product from a drug reference-list match and drops it straight into the cart —
   * the whole point of searching it mid-sale is to sell it, not just file it away. Goes through
   * the normal offline outbox, so it works (and queues) even without internet.
   */
  async addDrugToProducts(drug: EgyptianDrug): Promise<void> {
    this.addingDrugKey.set(drug.commercialNameEn);
    try {
      const created = await this.createProductFromDrug(drug);
      if (created) this.add(created);
    } finally {
      this.addingDrugKey.set(null);
    }
  }

  private async createProductFromDrug(drug: EgyptianDrug): Promise<PosProduct | null> {
    this.error.set(null);
    const pack = guessPack(drug);
    const id = newId();
    const nameEn = drug.commercialNameEn || drug.scientificName;
    const nameAr = drug.commercialNameAr || drug.commercialNameEn;
    const payload = {
      id,
      code: uniqueCode(buildProductCode(drug), this.sync.products().map((p) => p.code)),
      nameEn,
      nameAr,
      barcode: null,
      sellingPrice: Number(drug.priceEgp) || 0,
      stripsPerBox: pack?.strips ?? 1,
      unitsPerStrip: pack?.perStrip ?? 1,
      allowStripSale: false,
      allowUnitSale: false,
      stripPrice: null,
      unitPrice: null
    };

    const outcome = await this.sync.submit('product.create', payload, `${this.pick('New product', 'صنف جديد')}: ${nameEn}`);
    if (outcome.kind === 'rejected') {
      this.error.set(outcome.error);
      return null;
    }
    return this.sync.products().find((p) => p.id === id) ?? null;
  }

  onSearchEnter(): void {
    const raw = this.query().trim();
    const term = raw.toLowerCase();
    if (!term) return;
    // EAN-13 or GS1 DataMatrix (GTIN-14) from the box
    const candidates = barcodeCandidates(raw);
    const exact = this.sync
      .products()
      .find((p) => (p.barcode && candidates.includes(normalizeBarcode(p.barcode))) || p.code.toLowerCase() === term);
    const target = exact ?? (this.results().length === 1 ? this.results()[0] : null);
    if (target) this.add(target);
  }

  add(p: PosProduct, unit: SaleUnit = SaleUnit.Box): void {
    this.error.set(null);
    this.cart.update((lines) => {
      const existing = lines.find((l) => l.product.id === p.id && l.unit === unit);
      if (existing) return lines.map((l) => (l === existing ? { ...l, qty: l.qty + 1 } : l));
      return [...lines, { key: newId(), product: p, unit, qty: 1, price: unitPrice(p, unit) }];
    });
    this.query.set('');
    this.focusSearch();
  }

  setUnit(line: CartLine, unit: SaleUnit): void {
    this.updateLine(line, { unit, price: unitPrice(line.product, unit) });
  }

  setQty(line: CartLine, qty: number): void {
    if (qty <= 0) {
      this.remove(line);
      return;
    }
    this.updateLine(line, { qty: Math.round(qty * 100) / 100 });
  }

  /** Invalid prices (zero / negative) are kept as typed and block checkout with a message, instead of silently becoming 0. */
  setPrice(line: CartLine, price: number): void {
    this.updateLine(line, { price: round2(price) });
  }

  remove(line: CartLine): void {
    this.cart.update((lines) => lines.filter((l) => l.key !== line.key));
    const context = this.alternativesContext();
    if (context?.kind === 'cart' && context.line.key === line.key) this.alternativesContext.set(null);
  }

  isAlternativesOpenForLine(line: CartLine): boolean {
    const context = this.alternativesContext();
    return context?.kind === 'cart' && context.line.key === line.key;
  }

  isAlternativesOpenForResult(product: PosProduct): boolean {
    const context = this.alternativesContext();
    return context?.kind === 'search' && context.product.id === product.id;
  }

  toggleAlternatives(line: CartLine): void {
    this.alternativesContext.set(this.isAlternativesOpenForLine(line) ? null : { kind: 'cart', line });
  }

  toggleResultAlternatives(product: PosProduct): void {
    this.alternativesContext.set(this.isAlternativesOpenForResult(product) ? null : { kind: 'search', product });
  }

  /** Swaps the cart line, or adds a fresh line, to this other (same active ingredient) product. */
  useAlternative(alt: PosProduct): void {
    const context = this.alternativesContext();
    if (!context) return;
    if (context.kind === 'cart') {
      const unit = allowedUnits(alt).includes(context.line.unit) ? context.line.unit : SaleUnit.Box;
      this.updateLine(context.line, { product: alt, unit, price: unitPrice(alt, unit) });
    } else {
      this.add(alt);
    }
    this.alternativesContext.set(null);
  }

  /** A catalog-only equivalent (not yet one of this pharmacy's products) picked from the alternatives panel. */
  async addAlternativeFromCatalog(drug: EgyptianDrug): Promise<void> {
    if (!this.alternativesContext()) return;
    this.addingDrugKey.set(drug.commercialNameEn);
    try {
      const created = await this.createProductFromDrug(drug);
      if (created) this.useAlternative(created);
    } finally {
      this.addingDrugKey.set(null);
    }
  }

  canAddShortage(): boolean {
    return this.auth.hasPermission('Shortage.Create');
  }

  openShortage(product: PosProduct | null, name = ''): void {
    this.shortageDialog.set({ product, name });
  }

  onShortageSaved(message: string): void {
    this.shortageDialog.set(null);
    this.showToast(message);
    this.focusSearch();
  }

  closeShortage(): void {
    this.shortageDialog.set(null);
    this.focusSearch();
  }

  private showToast(message: string): void {
    this.toast.set(message);
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => this.toast.set(null), 3500);
  }

  selectCustomer(c: PosCustomer | null): void {
    this.customerId.set(c?.id ?? '');
    this.customerQuery.set('');
  }

  setCredit(): void {
    this.paidInput.set(0);
  }

  async createCustomer(): Promise<void> {
    const name = this.newCustomerName().trim();
    if (!name) return;
    const id = newId();
    const payload = {
      id,
      code: `C-${this.sync.deviceId}-${Date.now().toString(36).toUpperCase()}`,
      nameEn: name,
      nameAr: name,
      phone: this.newCustomerPhone().trim() || null,
      isActive: true,
      creditLimit: 0
    };
    const outcome = await this.sync.submit('customer.create', payload, `${this.pick('New customer', 'عميل جديد')}: ${name}`);
    if (outcome.kind === 'rejected') {
      this.error.set(outcome.error);
      return;
    }
    this.customerId.set(id);
    this.showNewCustomer.set(false);
    this.newCustomerName.set('');
    this.newCustomerPhone.set('');
  }

  async checkout(): Promise<void> {
    this.error.set(null);
    const lines = this.cart();
    const location = this.sync.locations().find((l) => l.id === this.locationId());
    if (!lines.length) return;
    const blocker = this.blocker();
    if (blocker) {
      this.error.set(blocker);
      return;
    }
    if (!location) {
      this.error.set(this.pick('Choose the branch first.', 'اختر الفرع أولاً.'));
      return;
    }
    if (this.due() > 0 && !this.customer()) {
      this.error.set(this.pick('Select a customer for credit (deferred) sales.', 'اختر العميل للبيع الآجل.'));
      return;
    }

    this.busy.set(true);
    const id = newId();
    // Peek only: the sequence advances once the sale is accepted or queued, so rejected attempts leave no gaps.
    const number = this.sync.peekInvoiceNumber();
    const soldAt = new Date().toISOString();
    const customer = this.customer();
    const payload = {
      id,
      number,
      soldAt,
      locationId: location.id,
      customerId: customer?.id ?? null,
      notes: this.notes().trim() || null,
      discountAmount: Math.max(0, this.discount() || 0),
      paidAmount: this.paid(),
      paymentMethod: this.paymentMethod(),
      items: lines.map((l) => ({
        productId: l.product.id,
        batchId: null,
        quantity: l.qty,
        unitPrice: l.price,
        unit: l.unit,
        baseQuantity: l.qty * unitFactor(l.product, l.unit)
      })),
      localDue: this.due()
    };

    try {
      const outcome = await this.sync.submit('sale.create', payload, `${this.pick('Invoice', 'فاتورة')} ${number} — ${this.total()}`);
      if (outcome.kind === 'rejected') {
        this.error.set(outcome.error);
        return;
      }
      this.sync.commitInvoiceNumber(number);

      const receipt: LocalReceipt = {
        id,
        number: outcome.kind === 'applied' ? outcome.result.number ?? number : number,
        soldAt,
        locationId: location.id,
        locationName: this.name(location),
        customerId: customer?.id ?? null,
        customerName: customer ? this.name(customer) : null,
        customerBalanceAfter: customer ? this.sync.customers().find((c) => c.id === customer.id)?.balance ?? null : null,
        lines: lines.map((l) => ({
          productId: l.product.id,
          nameEn: l.product.nameEn,
          nameAr: l.product.nameAr,
          unit: l.unit,
          quantity: l.qty,
          unitPrice: l.price,
          total: this.lineTotal(l)
        })),
        subTotal: this.subTotal(),
        discount: payload.discountAmount,
        total: this.total(),
        paid: this.paid(),
        due: this.due(),
        paymentMethod: this.paymentMethod(),
        cashier: this.auth.user()?.fullNameEn ?? null,
        synced: outcome.kind === 'applied',
        offline: outcome.kind === 'queued',
        warnings: outcome.kind === 'applied' ? outcome.result.warnings : []
      };
      await this.sync.saveReceipt(receipt);
      this.receipt.set(receipt);
      this.resetSale();
    } finally {
      this.busy.set(false);
    }
  }

  print(r: LocalReceipt): void {
    const pharmacy = this.auth.user()?.tenantCode ?? this.pick('Pharmacy', 'الصيدلية');
    printHtml(receiptHtml(r, this.lang(), pharmacy), {
      title: r.number,
      dir: this.lang() === 'ar' ? 'rtl' : 'ltr'
    });
  }

  closeReceipt(): void {
    this.receipt.set(null);
    this.focusSearch();
  }

  resetSale(): void {
    this.cart.set([]);
    this.customerId.set('');
    this.discount.set(0);
    this.paidInput.set(null);
    this.paymentMethod.set('Cash');
    this.notes.set('');
    this.alternativesContext.set(null);
  }

  private updateLine(line: CartLine, patch: Partial<CartLine>): void {
    this.cart.update((lines) => lines.map((l) => (l.key === line.key ? { ...l, ...patch } : l)));
  }

  private productById(id: string): PosProduct | undefined {
    return this.sync.products().find((p) => p.id === id);
  }
}

function normalizeDrugName(s: string): string {
  return s.toUpperCase().replace(/[^A-Z0-9؀-ۿ]+/g, ' ').trim();
}

/**
 * Only an exact (normalized) name match counts — this external catalog is matched by name
 * alone (no stable id links a product back to its catalog entry), so anything looser risks
 * suggesting the wrong drug's price.
 */
function pickCatalogMatch(product: PosProduct, matches: EgyptianDrug[]): EgyptianDrug | null {
  const target = normalizeDrugName(product.nameEn || product.nameAr);
  if (!target) return null;
  return (
    matches.find(
      (d) => normalizeDrugName(d.commercialNameEn) === target || normalizeDrugName(d.commercialNameAr) === target
    ) ?? null
  );
}

function normalizeScientificName(s: string): string {
  return s.toUpperCase().replace(/[^A-Z0-9؀-ۿ]+/g, ' ').trim();
}

export type AlternativeOption = { kind: 'own'; product: PosProduct } | { kind: 'catalog'; drug: EgyptianDrug };

/**
 * Equivalents (same active ingredient) for the selected product, found via the external drug
 * reference list since products here carry no scientific name of their own. Needs the selected
 * product to exactly match a catalog entry (same as the price check); if it doesn't, there's
 * nothing to safely compare against, so this returns nothing rather than guessing from the name
 * alone. Equivalents already in this pharmacy's own catalog come first (most in-stock first);
 * ones only in the reference list — not added as a product yet — come after, so they can be
 * added on the spot instead of just being hidden.
 */
function findAlternatives(product: PosProduct, ownProducts: PosProduct[], catalog: EgyptianDrug[]): AlternativeOption[] {
  const selfMatch = pickCatalogMatch(product, catalog);
  const scientific = selfMatch ? normalizeScientificName(selfMatch.scientificName) : '';
  if (!scientific) return [];

  const selfKey = normalizeDrugName(product.nameEn || product.nameAr);
  const seen = new Set<string>();
  const options: AlternativeOption[] = [];

  for (const drug of catalog) {
    if (normalizeScientificName(drug.scientificName) !== scientific) continue;
    const key = normalizeDrugName(drug.commercialNameEn || drug.commercialNameAr);
    if (!key || key === selfKey || seen.has(key)) continue;
    seen.add(key);

    const owned = ownProducts.find((p) => p.id !== product.id && normalizeDrugName(p.nameEn || p.nameAr) === key);
    options.push(owned ? { kind: 'own', product: owned } : { kind: 'catalog', drug });
  }

  const own = options.filter((o): o is { kind: 'own'; product: PosProduct } => o.kind === 'own');
  own.sort((a, b) => b.product.stock - a.product.stock);
  const catalogOnly = options.filter((o) => o.kind === 'catalog');
  return [...own, ...catalogOnly];
}
