import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { apiErrorMessage } from '../../core/http-error';
import { SyncService } from '../../core/offline/sync.service';
import { PosProduct } from '../../core/offline/models';
import { escapeHtml, printHtml } from '../../core/offline/print';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }

export type ShortageStatus = 'Open' | 'Ordered' | 'Received' | 'Cancelled';

export interface ShortageDto {
  id: string;
  productId: string | null;
  productCode: string | null;
  itemName: string;
  productNameEn: string | null;
  productNameAr: string | null;
  locationId: string | null;
  locationNameEn: string | null;
  locationNameAr: string | null;
  quantity: number;
  requestCount: number;
  status: ShortageStatus;
  notes: string | null;
  customerName: string | null;
  customerPhone: string | null;
  source: string;
  requestedAt: string;
  lastRequestedAt: string;
  resolvedAt: string | null;
  requestedByEn: string | null;
  requestedByAr: string | null;
  stockBoxes: number | null;
}

const STATUS_LABELS: Record<ShortageStatus, [string, string]> = {
  Open: ['Open', 'مطلوب'],
  Ordered: ['Ordered', 'تم الطلب من المورد'],
  Received: ['Received', 'تم التوفير'],
  Cancelled: ['Cancelled', 'ملغي']
};

@Component({
  selector: 'app-shortages',
  standalone: true,
  imports: [DatePipe, DecimalPipe],
  templateUrl: './shortages.html',
  styleUrl: './shortages.scss'
})
export class ShortagesPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/shortages`;
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly sync = inject(SyncService);

  readonly statuses: ShortageStatus[] = ['Open', 'Ordered', 'Received', 'Cancelled'];

  // ---------------------------------------------------------------- list / report state
  readonly items = signal<ShortageDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  readonly statusFilter = signal<string>('Active');
  readonly fromDate = signal('');
  readonly toDate = signal('');
  readonly locationFilter = signal('');
  readonly search = signal('');

  readonly summary = computed(() => {
    const list = this.items();
    return {
      items: list.length,
      requests: list.reduce((s, x) => s + x.requestCount, 0),
      open: list.filter((x) => x.status === 'Open').length,
      ordered: list.filter((x) => x.status === 'Ordered').length,
      notStocked: list.filter((x) => !x.productId).length
    };
  });

  // ---------------------------------------------------------------- add / edit form
  readonly editingId = signal<string | null>(null);
  readonly itemQuery = signal('');
  readonly selectedProduct = signal<PosProduct | null>(null);
  readonly quantity = signal(1);
  readonly formLocation = signal('');
  readonly customerName = signal('');
  readonly customerPhone = signal('');
  readonly notes = signal('');
  readonly formStatus = signal<ShortageStatus>('Open');
  readonly saving = signal(false);
  readonly suggestOpen = signal(false);

  readonly suggestions = computed(() => {
    const term = this.itemQuery().trim().toLowerCase();
    if (!term || this.selectedProduct()) return [];
    const words = term.split(/\s+/);
    return this.sync
      .products()
      .filter((p) => {
        const text = `${p.code} ${p.nameEn} ${p.nameAr} ${p.barcode ?? ''}`.toLowerCase();
        return words.every((w) => text.includes(w));
      })
      .slice(0, 8);
  });

  ngOnInit(): void {
    void this.sync.init();
    this.load();
  }

  pick(en: string, ar: string): string {
    return this.i18n.pick(en, ar);
  }

  statusLabel(status: string): string {
    const l = STATUS_LABELS[status as ShortageStatus];
    return l ? this.pick(l[0], l[1]) : status;
  }

  itemLabel(s: ShortageDto): string {
    if (!s.productId) return s.itemName;
    return this.i18n.language() === 'ar' ? s.productNameAr || s.itemName : s.productNameEn || s.itemName;
  }

  locationLabel(s: ShortageDto): string {
    return (this.i18n.language() === 'ar' ? s.locationNameAr || s.locationNameEn : s.locationNameEn) ?? '';
  }

  productName(p: PosProduct): string {
    return this.i18n.language() === 'ar' ? p.nameAr || p.nameEn : p.nameEn || p.nameAr;
  }

  locationName(l: { nameEn: string; nameAr: string }): string {
    return this.i18n.language() === 'ar' ? l.nameAr || l.nameEn : l.nameEn || l.nameAr;
  }

  // ---------------------------------------------------------------- load

  load(): void {
    const params: Record<string, string> = {};
    if (this.statusFilter()) params['status'] = this.statusFilter();
    if (this.fromDate()) params['from'] = new Date(`${this.fromDate()}T00:00:00`).toISOString();
    if (this.toDate()) params['to'] = new Date(`${this.toDate()}T23:59:59.999`).toISOString();
    if (this.locationFilter()) params['locationId'] = this.locationFilter();
    if (this.search().trim()) params['search'] = this.search().trim();

    this.loading.set(true);
    this.error.set(null);
    this.http.get<ApiResponse<ShortageDto[]>>(this.base, { params }).subscribe({
      next: (res) => {
        this.items.set(res.data ?? []);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(apiErrorMessage(err, this.pick('Unable to load shortages.', 'تعذر تحميل النواقص.')));
      }
    });
  }

  resetFilters(): void {
    this.statusFilter.set('Active');
    this.fromDate.set('');
    this.toDate.set('');
    this.locationFilter.set('');
    this.search.set('');
    this.load();
  }

  // ---------------------------------------------------------------- form

  onItemInput(value: string): void {
    this.itemQuery.set(value);
    this.selectedProduct.set(null);
    this.suggestOpen.set(true);
  }

  chooseProduct(p: PosProduct): void {
    this.selectedProduct.set(p);
    this.itemQuery.set(this.productName(p));
    this.suggestOpen.set(false);
  }

  startEdit(s: ShortageDto): void {
    this.editingId.set(s.id);
    this.selectedProduct.set(null);
    this.itemQuery.set(this.itemLabel(s));
    this.quantity.set(s.quantity);
    this.formLocation.set(s.locationId ?? '');
    this.customerName.set(s.customerName ?? '');
    this.customerPhone.set(s.customerPhone ?? '');
    this.notes.set(s.notes ?? '');
    this.formStatus.set(s.status);
    this.notice.set(null);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  clearForm(): void {
    this.editingId.set(null);
    this.itemQuery.set('');
    this.selectedProduct.set(null);
    this.quantity.set(1);
    this.customerName.set('');
    this.customerPhone.set('');
    this.notes.set('');
    this.formStatus.set('Open');
    this.suggestOpen.set(false);
  }

  save(): void {
    const quantity = Number(this.quantity());
    if (!(quantity > 0)) {
      this.error.set(this.pick('Quantity must be at least 1.', 'الكمية يجب أن تكون 1 على الأقل.'));
      return;
    }
    const editingId = this.editingId();
    const done = (message: string) => {
      this.saving.set(false);
      this.notice.set(message);
      this.clearForm();
      this.load();
    };
    const fail = (err: unknown) => {
      this.saving.set(false);
      this.error.set(apiErrorMessage(err, this.pick('Save failed.', 'فشل الحفظ.')));
    };

    this.error.set(null);
    this.saving.set(true);

    if (editingId) {
      this.http
        .put(`${this.base}/${editingId}`, {
          quantity,
          status: this.formStatus(),
          notes: this.notes().trim() || null,
          customerName: this.customerName().trim() || null,
          customerPhone: this.customerPhone().trim() || null
        })
        .subscribe({ next: () => done(this.pick('Shortage updated.', 'تم تحديث الصنف.')), error: fail });
      return;
    }

    const product = this.selectedProduct();
    const name = this.itemQuery().trim();
    if (!product && !name) {
      this.saving.set(false);
      this.error.set(this.pick('Enter the item name or choose a product.', 'اكتب اسم الصنف أو اختر صنفاً من القائمة.'));
      return;
    }
    this.http
      .post<ApiResponse<{ merged: boolean; requestCount: number }>>(this.base, {
        productId: product?.id ?? null,
        itemName: product ? null : name,
        quantity,
        locationId: this.formLocation() || null,
        customerName: this.customerName().trim() || null,
        customerPhone: this.customerPhone().trim() || null,
        notes: this.notes().trim() || null,
        source: 'Manual'
      })
      .subscribe({
        next: (res) =>
          done(
            res.data?.merged
              ? this.pick(
                  `Already on the list — request count is now ${res.data.requestCount}.`,
                  `الصنف موجود بالفعل في النواقص — عدد مرات الطلب أصبح ${res.data.requestCount}.`
                )
              : this.pick('Added to shortages.', 'تمت الإضافة إلى النواقص.')
          ),
        error: fail
      });
  }

  // ---------------------------------------------------------------- row actions

  setStatus(s: ShortageDto, status: string): void {
    this.http
      .put(`${this.base}/${s.id}`, {
        quantity: s.quantity,
        status,
        notes: s.notes,
        customerName: s.customerName,
        customerPhone: s.customerPhone
      })
      .subscribe({
        next: () => this.load(),
        error: (err) => this.error.set(apiErrorMessage(err, this.pick('Update failed.', 'فشل التحديث.')))
      });
  }

  remove(s: ShortageDto): void {
    if (!confirm(this.pick(`Delete "${this.itemLabel(s)}" from shortages?`, `حذف «${this.itemLabel(s)}» من النواقص؟`))) return;
    this.http.delete(`${this.base}/${s.id}`).subscribe({
      next: () => {
        if (this.editingId() === s.id) this.clearForm();
        this.load();
      },
      error: (err) => this.error.set(apiErrorMessage(err, this.pick('Delete failed.', 'فشل الحذف.')))
    });
  }

  // ---------------------------------------------------------------- print

  print(): void {
    const ar = this.i18n.language() === 'ar';
    const L = (en: string, a: string) => (ar ? a : en);
    const fmtDate = (iso: string) =>
      new Date(iso).toLocaleString(ar ? 'ar-EG' : 'en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    const num = (n: number) => (Math.round(n * 100) / 100).toString();

    const filterParts: string[] = [
      `${L('Status', 'الحالة')}: ${this.statusFilter() === 'Active' ? L('Open + ordered', 'المطلوب + تم طلبه') : this.statusFilter() ? this.statusLabel(this.statusFilter()) : L('All', 'الكل')}`
    ];
    if (this.fromDate() || this.toDate()) {
      filterParts.push(`${L('Period', 'الفترة')}: ${this.fromDate() || '…'} → ${this.toDate() || '…'}`);
    }
    if (this.locationFilter()) {
      const loc = this.sync.locations().find((l) => l.id === this.locationFilter());
      if (loc) filterParts.push(`${L('Branch', 'الفرع')}: ${this.locationName(loc)}`);
    }
    if (this.search().trim()) filterParts.push(`${L('Search', 'بحث')}: ${this.search().trim()}`);

    const rows = this.items()
      .map(
        (s, i) => `<tr>
          <td class="num">${i + 1}</td>
          <td><b>${escapeHtml(this.itemLabel(s))}</b>${s.productCode ? `<br><span class="muted" dir="ltr">${escapeHtml(s.productCode)}</span>` : `<br><span class="muted">${L('Not stocked', 'غير موجود بالأصناف')}</span>`}</td>
          <td class="num">${num(s.quantity)}</td>
          <td class="num">${s.requestCount}</td>
          <td class="num">${s.stockBoxes === null ? '—' : num(s.stockBoxes)}</td>
          <td>${escapeHtml(s.customerName ?? '')}${s.customerPhone ? `<br><span dir="ltr">${escapeHtml(s.customerPhone)}</span>` : ''}</td>
          <td>${escapeHtml(fmtDate(s.lastRequestedAt))}</td>
          <td>${escapeHtml(this.statusLabel(s.status))}</td>
          <td>${escapeHtml(s.notes ?? '')}</td>
          <td class="check"></td>
        </tr>`
      )
      .join('');

    const sum = this.summary();
    printHtml(
      `<h1>${L('Shortages report', 'تقرير النواقص')}</h1>
       <div class="center">${escapeHtml(this.auth.user()?.tenantCode ?? '')} — ${L('Printed', 'تاريخ الطباعة')}: ${escapeHtml(fmtDate(new Date().toISOString()))}</div>
       <div class="center muted">${escapeHtml(filterParts.join(' · '))}</div>
       <hr>
       <div class="grid">
         <div class="box">${L('Items', 'عدد الأصناف')}: <b>${sum.items}</b></div>
         <div class="box">${L('Total requests', 'إجمالي مرات الطلب')}: <b>${sum.requests}</b></div>
         <div class="box">${L('Open', 'مطلوب')}: <b>${sum.open}</b></div>
         <div class="box">${L('Ordered from supplier', 'تم الطلب من المورد')}: <b>${sum.ordered}</b></div>
       </div>
       <table class="report">
         <thead><tr>
           <th class="num">#</th><th>${L('Item', 'الصنف')}</th><th class="num">${L('Qty (box)', 'الكمية (علبة)')}</th>
           <th class="num">${L('Requests', 'مرات الطلب')}</th><th class="num">${L('On hand', 'المتاح')}</th>
           <th>${L('Customer', 'العميل')}</th><th>${L('Last request', 'آخر طلب')}</th><th>${L('Status', 'الحالة')}</th>
           <th>${L('Notes', 'ملاحظات')}</th><th>✓</th>
         </tr></thead>
         <tbody>${rows || `<tr><td colspan="10" class="center">${L('No shortages.', 'لا توجد نواقص.')}</td></tr>`}</tbody>
       </table>
       <style>
         body { width: auto; font-size: 11px; }
         table.report { margin-top: 8px; }
         table.report th, table.report td { border: 1px solid #000; padding: 4px; }
         td.check { width: 22px; }
         tr { page-break-inside: avoid; }
       </style>`,
      { title: `shortages-${new Date().toISOString().slice(0, 10)}`, dir: ar ? 'rtl' : 'ltr', paperWidth: 'A4' }
    );
  }
}
