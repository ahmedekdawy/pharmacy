import { DecimalPipe } from '@angular/common';
import { Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { SyncService } from '../../core/offline/sync.service';
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
  imports: [DecimalPipe, BarcodeScanner],
  templateUrl: './pos.html',
  styleUrl: './pos.scss'
})
export class PosPage implements OnInit {
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly sync = inject(SyncService);
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
  readonly cartOpen = signal(false); // mobile bottom sheet

  readonly lang = computed(() => this.i18n.language());

  readonly results = computed(() => {
    const term = this.query().trim().toLowerCase();
    const all = this.sync.products();
    if (!term) return [];
    return all
      .filter(
        (p) =>
          p.code.toLowerCase().includes(term) ||
          p.nameEn.toLowerCase().includes(term) ||
          p.nameAr.includes(term) ||
          (p.barcode ?? '').toLowerCase() === term
      )
      .slice(0, 30);
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
  readonly total = computed(() => round2(Math.max(0, this.subTotal() - Math.min(this.discount() || 0, this.subTotal()))));
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
    this.searchBox()?.nativeElement.focus();
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

  setPrice(line: CartLine, price: number): void {
    this.updateLine(line, { price: Math.max(0, price) });
  }

  remove(line: CartLine): void {
    this.cart.update((lines) => lines.filter((l) => l.key !== line.key));
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
    const number = this.sync.nextInvoiceNumber();
    const soldAt = new Date().toISOString();
    const customer = this.customer();
    const payload = {
      id,
      number,
      soldAt,
      locationId: location.id,
      customerId: customer?.id ?? null,
      notes: this.notes().trim() || null,
      discountAmount: Math.min(this.discount() || 0, this.subTotal()),
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
    const pharmacy = this.auth.user()?.tenantCode ?? 'Pharmacy';
    printHtml(receiptHtml(r, this.lang(), pharmacy), {
      title: r.number,
      dir: this.lang() === 'ar' ? 'rtl' : 'ltr'
    });
  }

  closeReceipt(): void {
    this.receipt.set(null);
    this.searchBox()?.nativeElement.focus();
  }

  resetSale(): void {
    this.cart.set([]);
    this.customerId.set('');
    this.discount.set(0);
    this.paidInput.set(null);
    this.paymentMethod.set('Cash');
    this.notes.set('');
    this.cartOpen.set(false);
  }

  private updateLine(line: CartLine, patch: Partial<CartLine>): void {
    this.cart.update((lines) => lines.map((l) => (l.key === line.key ? { ...l, ...patch } : l)));
  }

  private productById(id: string): PosProduct | undefined {
    return this.sync.products().find((p) => p.id === id);
  }
}
