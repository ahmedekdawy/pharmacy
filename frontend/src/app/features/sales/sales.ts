import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { LocalReceipt, PaymentMethod, SaleUnit } from '../../core/offline/models';
import { printHtml, receiptHtml } from '../../core/offline/print';
import { apiErrorMessage } from '../../core/http-error';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface SaleDto {
  id: string; number: string; locationNameEn: string; customerNameEn?: string | null; customerNameAr?: string | null;
  soldAt: string; subTotal: number; discountAmount: number; totalAmount: number; paidAmount: number; dueAmount: number;
  paymentMethod: string; source: string; hasStockWarning: boolean;
}
interface SaleDetail {
  id: string; number: string; locationId: string; locationNameEn: string; soldAt: string; totalAmount: number;
  customerId?: string | null; customerNameEn?: string | null; customerNameAr?: string | null;
  subTotal: number; discountAmount: number; paidAmount: number; dueAmount: number; paymentMethod: string; source: string;
  items: { productId: string; productNameEn: string; productNameAr: string; unit: SaleUnit; quantity: number; unitPrice: number; lineTotal: number }[];
}

/** Invoice history (selling itself happens in the POS screen). */
@Component({
  selector: 'app-sales',
  standalone: true,
  imports: [DecimalPipe, DatePipe, RouterLink],
  template: `
  <section class="page">
    <header class="head">
      <h1>{{ pick('Invoices', 'الفواتير') }}</h1>
      <div class="filters">
        <label class="field">
          <span class="field-label">{{ pick('From', 'من') }}</span>
          <input type="date" [value]="from()" (change)="from.set($any($event.target).value); reload()" />
        </label>
        <label class="field">
          <span class="field-label">{{ pick('To', 'إلى') }}</span>
          <input type="date" [value]="to()" (change)="to.set($any($event.target).value); reload()" />
        </label>
        @if (auth.hasPermission('Sale.Create')) {
          <a routerLink="/app/pos" class="btn primary">{{ pick('Open POS', 'نقطة البيع') }}</a>
        }
      </div>
    </header>
    @if (error()) { <p class="error">{{ error() }}</p> }
    <p class="sum">{{ items().length }} {{ pick('invoices', 'فاتورة') }} · {{ pick('Net', 'الصافي') }} <b>{{ total() | number: '1.2-2' }}</b>
      · {{ pick('On account', 'آجل') }} <b class="debt">{{ due() | number: '1.2-2' }}</b></p>
    <div class="table-wrap">
      <table>
        <thead><tr>
          <th>{{ pick('Date', 'التاريخ') }}</th><th>{{ pick('No.', 'الرقم') }}</th><th>{{ pick('Customer', 'العميل') }}</th>
          <th class="num">{{ pick('Net', 'الصافي') }}</th><th class="num">{{ pick('Paid', 'المدفوع') }}</th><th class="num">{{ pick('On account', 'آجل') }}</th><th></th>
        </tr></thead>
        <tbody>
          @for (s of items(); track s.id) {
            <tr [class.flag]="s.hasStockWarning">
              <td>{{ s.soldAt | date: 'yyyy-MM-dd HH:mm' }}</td>
              <td>{{ s.number }} @if (s.source === 'Offline') { <span class="tag">{{ pick('offline', 'بدون إنترنت') }}</span> }</td>
              <td>{{ (lang() === 'ar' ? s.customerNameAr : s.customerNameEn) || pick('Walk-in', 'نقدي') }}</td>
              <td class="num">{{ s.totalAmount | number: '1.2-2' }}</td>
              <td class="num">{{ s.paidAmount | number: '1.2-2' }}</td>
              <td class="num">{{ s.dueAmount ? (s.dueAmount | number: '1.2-2') : '' }}</td>
              <td><button type="button" class="btn" (click)="reprint(s)">{{ pick('Print', 'طباعة') }}</button></td>
            </tr>
          } @empty { <tr><td colspan="7" class="hint">{{ pick('No invoices.', 'لا توجد فواتير.') }}</td></tr> }
        </tbody>
      </table>
    </div>
  </section>`,
  styles: [`
    :host { display:block; padding:1rem; color:var(--text); }
    .page { max-width:72rem; margin:0 auto; display:grid; gap:.75rem; }
    .head { display:flex; flex-wrap:wrap; justify-content:space-between; align-items:center; gap:.6rem; } h1 { margin:0; }
    .filters { display:flex; flex-wrap:wrap; gap:.4rem; align-items:flex-end; }
    input { padding:.55rem .7rem; border:1px solid var(--border); border-radius:.45rem; background:var(--surface); color:var(--text); font:inherit; }
    .btn { border:1px solid var(--border); background:var(--surface-muted); color:var(--text); border-radius:.45rem; padding:.45rem .8rem; font:inherit; cursor:pointer; text-decoration:none; }
    .btn.primary { background:var(--accent); color:#fff; border-color:transparent; }
    .table-wrap { overflow-x:auto; background:var(--surface); border:1px solid var(--border); border-radius:.6rem; }
    table { width:100%; border-collapse:collapse; font-size:.9rem; }
    th, td { padding:.5rem; border-bottom:1px solid var(--border); text-align:start; white-space:nowrap; }
    .num { text-align:end; } tr.flag td { background:rgba(217,164,65,.12); }
    .tag { font-size:.7rem; background:#f6e3c8; color:#8a4b08; border-radius:999px; padding:0 .4rem; }
    .debt { color:#b5462f; } .error { color:#c45c4a; } .hint { color:var(--muted); } .sum { margin:0; }
  `]
})
export class SalesPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly items = signal<SaleDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly from = signal(dateOnly(new Date(Date.now() - 6 * 86_400_000)));
  readonly to = signal(dateOnly(new Date()));
  readonly lang = computed(() => this.i18n.language());
  readonly total = computed(() => this.items().reduce((s, x) => s + x.totalAmount, 0));
  readonly due = computed(() => this.items().reduce((s, x) => s + x.dueAmount, 0));

  ngOnInit(): void {
    this.reload();
  }

  pick(en: string, ar: string): string {
    return this.i18n.pick(en, ar);
  }

  reload(): void {
    const from = new Date(`${this.from()}T00:00:00`).toISOString();
    const to = new Date(new Date(`${this.to()}T00:00:00`).getTime() + 86_400_000).toISOString();
    this.http.get<ApiResponse<SaleDto[]>>(`${environment.apiBaseUrl}/sales`, { params: { from, to, take: 1000 } }).subscribe({
      next: (res) => { this.items.set(res.data ?? []); this.error.set(null); },
      error: () => this.error.set(this.pick('Unable to load sales (offline?).', 'تعذر تحميل الفواتير (لا يوجد اتصال؟).'))
    });
  }

  reprint(s: SaleDto): void {
    this.http.get<ApiResponse<SaleDetail>>(`${environment.apiBaseUrl}/sales/${s.id}`).subscribe({
      next: (res) => {
        const d = res.data;
        const ar = this.lang() === 'ar';
        const receipt: LocalReceipt = {
          id: d.id, number: d.number, soldAt: d.soldAt, locationId: d.locationId, locationName: d.locationNameEn,
          customerId: d.customerId, customerName: ar ? d.customerNameAr : d.customerNameEn,
          lines: d.items.map((i) => ({ productId: i.productId, nameEn: i.productNameEn, nameAr: i.productNameAr, unit: i.unit, quantity: i.quantity, unitPrice: i.unitPrice, total: i.lineTotal })),
          subTotal: d.subTotal, discount: d.discountAmount, total: d.totalAmount, paid: d.paidAmount, due: d.dueAmount,
          paymentMethod: d.paymentMethod as PaymentMethod, synced: true, offline: false
        };
        printHtml(receiptHtml(receipt, this.lang(), this.auth.user()?.tenantCode ?? ''), { title: d.number, dir: ar ? 'rtl' : 'ltr' });
      },
      error: (err) => this.error.set(apiErrorMessage(err, this.pick('Unable to load the invoice for printing.', 'تعذر تحميل الفاتورة للطباعة.')))
    });
  }
}

function dateOnly(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
