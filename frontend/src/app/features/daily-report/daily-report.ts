import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { SyncService } from '../../core/offline/sync.service';
import { LocalReceipt, SaleUnit, round2, unitLabel } from '../../core/offline/models';
import { escapeHtml, money, printHtml, receiptHtml } from '../../core/offline/print';
import { apiErrorMessage } from '../../core/http-error';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }

interface DailyInvoice {
  id: string; number: string; soldAt: string; customerNameEn?: string | null; customerNameAr?: string | null;
  subTotal: number; discountAmount: number; totalAmount: number; paidAmount: number; dueAmount: number;
  paymentMethod: string; source: string; hasStockWarning: boolean;
}
interface DailyItem { productId: string; productCode: string; productNameEn: string; productNameAr: string; unit: SaleUnit; quantity: number; total: number; }
interface DailyPayment { id: string; number: string; paidAt: string; customerNameEn: string; customerNameAr: string; amount: number; paymentMethod: string; }
interface DailyReport {
  date: string; invoiceCount: number; offlineInvoiceCount: number; stockWarningCount: number;
  subTotal: number; discountTotal: number; netSales: number; cashSales: number; cardSales: number; walletSales: number;
  creditSales: number; debtCollected: number; debtCollectedCash: number; returnsTotal: number; returnsRefunded: number;
  returnsCredited: number; expensesTotal: number; cashInDrawer: number;
  invoices: DailyInvoice[]; items: DailyItem[]; payments: DailyPayment[];
}

@Component({
  selector: 'app-daily-report',
  standalone: true,
  imports: [DecimalPipe, DatePipe],
  template: `
  <section class="page">
    <header class="head">
      <h1>{{ pick('Daily invoice', 'اليومية') }}</h1>
      <div class="filters">
        <input type="date" [value]="date()" (change)="setDate($any($event.target).value)" />
        <select [value]="locationId()" (change)="locationId.set($any($event.target).value); load()">
          <option value="">{{ pick('All branches', 'كل الفروع') }}</option>
          @for (l of sync.locations(); track l.id) { <option [value]="l.id">{{ lang() === 'ar' ? l.nameAr : l.nameEn }}</option> }
        </select>
        <button type="button" class="btn" (click)="load()">{{ pick('Refresh', 'تحديث') }}</button>
        @if (report()) { <button type="button" class="btn primary" (click)="printReport()">{{ pick('Print', 'طباعة') }}</button> }
      </div>
    </header>

    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (sync.pendingCount()) {
      <p class="warn">{{ sync.pendingCount() }} {{ pick('operation(s) on this device are not uploaded yet and are not included below.', 'عملية على هذا الجهاز لم تُرفع بعد وغير محسوبة بالأسفل.') }}</p>
    }

    @if (report(); as r) {
      <div class="cards">
        <div class="card main"><span>{{ pick('Net sales', 'صافي المبيعات') }}</span><b>{{ r.netSales | number: '1.2-2' }}</b><small>{{ r.invoiceCount }} {{ pick('invoices', 'فاتورة') }}</small></div>
        <div class="card"><span>{{ pick('Cash in drawer', 'النقدية بالدرج') }}</span><b>{{ r.cashInDrawer | number: '1.2-2' }}</b></div>
        <div class="card"><span>{{ pick('Cash sales', 'مبيعات نقدية') }}</span><b>{{ r.cashSales | number: '1.2-2' }}</b></div>
        <div class="card"><span>{{ pick('Card / wallet', 'فيزا / محفظة') }}</span><b>{{ r.cardSales + r.walletSales | number: '1.2-2' }}</b></div>
        <div class="card debt"><span>{{ pick('Credit sales (on account)', 'مبيعات آجلة') }}</span><b>{{ r.creditSales | number: '1.2-2' }}</b></div>
        <div class="card"><span>{{ pick('Debt collected', 'تحصيل مديونيات') }}</span><b>{{ r.debtCollected | number: '1.2-2' }}</b></div>
        <div class="card"><span>{{ pick('Discounts', 'الخصومات') }}</span><b>{{ r.discountTotal | number: '1.2-2' }}</b></div>
        <div class="card"><span>{{ pick('Returns', 'المرتجعات') }}</span><b>{{ r.returnsTotal | number: '1.2-2' }}</b></div>
        <div class="card"><span>{{ pick('Expenses', 'المصروفات') }}</span><b>{{ r.expensesTotal | number: '1.2-2' }}</b></div>
      </div>
      @if (r.offlineInvoiceCount || r.stockWarningCount) {
        <p class="warn">
          {{ r.offlineInvoiceCount }} {{ pick('invoice(s) were made offline', 'فاتورة تمت بدون إنترنت') }}
          @if (r.stockWarningCount) { · {{ r.stockWarningCount }} {{ pick('with stock shortage – review inventory', 'بها عجز مخزون – راجع المخزن') }} }
        </p>
      }

      <h2>{{ pick('Invoices', 'الفواتير') }}</h2>
      <div class="table-wrap">
        <table>
          <thead><tr>
            <th>{{ pick('Time', 'الوقت') }}</th><th>{{ pick('No.', 'الرقم') }}</th><th>{{ pick('Customer', 'العميل') }}</th>
            <th class="num">{{ pick('Net', 'الصافي') }}</th><th class="num">{{ pick('Paid', 'المدفوع') }}</th><th class="num">{{ pick('On account', 'آجل') }}</th><th>{{ pick('Method', 'الدفع') }}</th>
          </tr></thead>
          <tbody>
            @for (i of r.invoices; track i.id) {
              <tr [class.flag]="i.hasStockWarning">
                <td>{{ i.soldAt | date: 'HH:mm' }}</td>
                <td>{{ i.number }} @if (i.source === 'Offline') { <span class="tag">offline</span> }</td>
                <td>{{ (lang() === 'ar' ? i.customerNameAr : i.customerNameEn) || pick('Walk-in', 'نقدي') }}</td>
                <td class="num">{{ i.totalAmount | number: '1.2-2' }}</td>
                <td class="num">{{ i.paidAmount | number: '1.2-2' }}</td>
                <td class="num">{{ i.dueAmount ? (i.dueAmount | number: '1.2-2') : '' }}</td>
                <td>{{ methodName(i.paymentMethod) }}</td>
              </tr>
            } @empty { <tr><td colspan="7" class="hint">{{ pick('No invoices.', 'لا توجد فواتير.') }}</td></tr> }
          </tbody>
        </table>
      </div>

      <h2>{{ pick('Items sold', 'الأصناف المباعة') }}</h2>
      <div class="table-wrap">
        <table>
          <thead><tr><th>{{ pick('Item', 'الصنف') }}</th><th class="num">{{ pick('Qty', 'الكمية') }}</th><th>{{ pick('Unit', 'الوحدة') }}</th><th class="num">{{ pick('Total', 'الإجمالي') }}</th></tr></thead>
          <tbody>
            @for (i of r.items; track $index) {
              <tr><td>{{ lang() === 'ar' ? i.productNameAr : i.productNameEn }}</td><td class="num">{{ i.quantity | number: '1.0-2' }}</td><td>{{ unitName(i.unit) }}</td><td class="num">{{ i.total | number: '1.2-2' }}</td></tr>
            }
          </tbody>
        </table>
      </div>

      @if (r.payments.length) {
        <h2>{{ pick('Debt payments', 'سداد المديونيات') }}</h2>
        <div class="table-wrap">
          <table>
            <tbody>
              @for (p of r.payments; track p.id) {
                <tr><td>{{ p.paidAt | date: 'HH:mm' }}</td><td>{{ p.number }}</td><td>{{ lang() === 'ar' ? p.customerNameAr : p.customerNameEn }}</td><td>{{ methodName(p.paymentMethod) }}</td><td class="num">{{ p.amount | number: '1.2-2' }}</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
    }

    @if (!report() && localReceipts().length) {
      <h2>{{ pick('Invoices on this device (offline)', 'فواتير هذا الجهاز (بدون إنترنت)') }}</h2>
      <p class="hint">{{ pick('Total', 'الإجمالي') }}: <b>{{ localTotal() | number: '1.2-2' }}</b> · {{ localReceipts().length }} {{ pick('invoices', 'فاتورة') }}</p>
    }
    @if (localReceipts().length) {
      <details>
        <summary>{{ pick('Reprint a receipt from this device', 'إعادة طباعة إيصال من هذا الجهاز') }}</summary>
        <ul class="receipts">
          @for (r of localReceipts(); track r.id) {
            <li>
              <span>{{ r.soldAt | date: 'HH:mm' }} · {{ r.number }} · {{ r.total | number: '1.2-2' }}</span>
              @if (!r.synced) { <span class="tag">{{ pick('pending', 'لم تُرفع') }}</span> }
              <button type="button" class="btn" (click)="reprint(r)">{{ pick('Print', 'طباعة') }}</button>
            </li>
          }
        </ul>
      </details>
    }
  </section>`,
  styles: [`
    :host { display:block; padding:1rem; color:var(--text); }
    .page { max-width:72rem; margin:0 auto; display:grid; gap:.8rem; }
    .head { display:flex; flex-wrap:wrap; justify-content:space-between; align-items:center; gap:.75rem; }
    h1, h2 { margin:0; } h2 { font-size:1.05rem; margin-top:.5rem; }
    .filters { display:flex; flex-wrap:wrap; gap:.4rem; }
    input, select { padding:.55rem .7rem; border:1px solid var(--border); border-radius:.45rem; background:var(--surface); color:var(--text); font:inherit; }
    .btn { border:1px solid var(--border); background:var(--surface-muted); color:var(--text); border-radius:.45rem; padding:.5rem .8rem; font:inherit; cursor:pointer; }
    .btn.primary { background:var(--accent); color:#fff; border-color:transparent; }
    .cards { display:grid; grid-template-columns:repeat(auto-fill,minmax(10.5rem,1fr)); gap:.6rem; }
    .card { background:var(--surface); border:1px solid var(--border); border-radius:.65rem; padding:.75rem; display:flex; flex-direction:column; gap:.2rem;
      span { color:var(--muted); font-size:.82rem; } b { font-size:1.25rem; } small { color:var(--muted); } }
    .card.main { background:var(--accent); color:#fff; span, small { color:rgba(255,255,255,.85); } }
    .card.debt b { color:#b5462f; }
    .table-wrap { overflow-x:auto; background:var(--surface); border:1px solid var(--border); border-radius:.6rem; }
    table { width:100%; border-collapse:collapse; font-size:.9rem; }
    th, td { padding:.5rem; border-bottom:1px solid var(--border); text-align:start; white-space:nowrap; }
    .num { text-align:end; }
    tr.flag td { background:rgba(217,164,65,.12); }
    .tag { font-size:.7rem; background:#f6e3c8; color:#8a4b08; border-radius:999px; padding:0 .4rem; margin-inline-start:.3rem; }
    .warn { color:#8a4b08; margin:0; } .error { color:#c45c4a; } .hint { color:var(--muted); }
    .receipts { list-style:none; padding:0; display:grid; gap:.35rem; li { display:flex; gap:.5rem; align-items:center; justify-content:space-between; } }
  `]
})
export class DailyReportPage implements OnInit {
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly sync = inject(SyncService);
  private readonly http = inject(HttpClient);

  readonly date = signal(toLocalDate(new Date()));
  readonly locationId = signal('');
  readonly report = signal<DailyReport | null>(null);
  readonly error = signal<string | null>(null);
  readonly localReceipts = signal<LocalReceipt[]>([]);
  readonly lang = computed(() => this.i18n.language());
  readonly localTotal = computed(() => round2(this.localReceipts().reduce((s, r) => s + r.total, 0)));

  ngOnInit(): void {
    void this.sync.init();
    this.load();
  }

  pick(en: string, ar: string): string {
    return this.i18n.pick(en, ar);
  }

  unitName(u: SaleUnit): string {
    return unitLabel(u, this.lang());
  }

  methodName(m: string): string {
    return ({ Cash: this.pick('Cash', 'نقدي'), Card: this.pick('Card', 'فيزا'), Wallet: this.pick('Wallet', 'محفظة') } as Record<string, string>)[m] ?? m;
  }

  setDate(value: string): void {
    this.date.set(value);
    this.load();
  }

  load(): void {
    this.error.set(null);
    void this.loadLocal();
    const params: Record<string, string | number> = {
      date: this.date(),
      utcOffsetMinutes: -new Date(`${this.date()}T12:00:00`).getTimezoneOffset()
    };
    if (this.locationId()) params['locationId'] = this.locationId();
    this.http.get<ApiResponse<DailyReport>>(`${environment.apiBaseUrl}/reports/daily`, { params }).subscribe({
      next: (res) => this.report.set(res.data),
      error: (err) => {
        this.report.set(null);
        this.error.set(
          err?.status === 0
            ? this.pick('No internet connection – showing invoices saved on this device only.', 'لا يوجد اتصال – يتم عرض فواتير هذا الجهاز فقط.')
            : apiErrorMessage(err, this.pick('Unable to load the report.', 'تعذر تحميل التقرير.'))
        );
      }
    });
  }

  reprint(r: LocalReceipt): void {
    printHtml(receiptHtml(r, this.lang(), this.auth.user()?.tenantCode ?? ''), { title: r.number, dir: this.lang() === 'ar' ? 'rtl' : 'ltr' });
  }

  printReport(): void {
    const r = this.report();
    if (!r) return;
    const L = (en: string, ar: string) => this.pick(en, ar);
    const ar = this.lang() === 'ar';
    const line = (label: string, value: number) => `<tr><td>${label}</td><td class="num">${money(value)}</td></tr>`;
    const invoices = r.invoices
      .map((i) => `<tr><td>${new Date(i.soldAt).toLocaleTimeString(ar ? 'ar-EG' : 'en-GB', { hour: '2-digit', minute: '2-digit' })}</td><td>${escapeHtml(i.number)}</td><td class="num">${money(i.totalAmount)}</td><td class="num">${i.dueAmount ? money(i.dueAmount) : ''}</td></tr>`)
      .join('');
    const items = r.items
      .map((i) => `<tr><td>${escapeHtml(ar ? i.productNameAr : i.productNameEn)}</td><td class="num">${i.quantity} ${unitLabel(i.unit, this.lang())}</td><td class="num">${money(i.total)}</td></tr>`)
      .join('');
    printHtml(
      `<h1>${L('Daily invoice', 'اليومية')}</h1>
       <div class="center">${escapeHtml(this.auth.user()?.tenantCode ?? '')} — ${escapeHtml(r.date)}</div><hr>
       <table class="totals">
         ${line(L('Invoices', 'عدد الفواتير'), r.invoiceCount).replace('.00', '')}
         ${line(L('Gross sales', 'إجمالي المبيعات'), r.subTotal)}
         ${line(L('Discounts', 'الخصومات'), r.discountTotal)}
         <tr class="grand"><td>${L('Net sales', 'صافي المبيعات')}</td><td class="num">${money(r.netSales)}</td></tr>
         ${line(L('Cash', 'نقدي'), r.cashSales)}
         ${line(L('Card', 'فيزا'), r.cardSales)}
         ${line(L('Wallet', 'محفظة'), r.walletSales)}
         ${line(L('On account', 'آجل'), r.creditSales)}
         ${line(L('Debt collected', 'تحصيل مديونيات'), r.debtCollected)}
         ${line(L('Returns refunded', 'مرتجعات نقدية'), r.returnsRefunded)}
         ${line(L('Expenses', 'مصروفات'), r.expensesTotal)}
         <tr class="grand"><td>${L('Cash in drawer', 'النقدية بالدرج')}</td><td class="num">${money(r.cashInDrawer)}</td></tr>
       </table>
       <h2>${L('Invoices', 'الفواتير')}</h2>
       <table><thead><tr><th>${L('Time', 'الوقت')}</th><th>${L('No.', 'الرقم')}</th><th class="num">${L('Net', 'الصافي')}</th><th class="num">${L('Credit', 'آجل')}</th></tr></thead><tbody>${invoices}</tbody></table>
       <h2>${L('Items', 'الأصناف')}</h2>
       <table><tbody>${items}</tbody></table>`,
      { title: `daily-${r.date}`, dir: ar ? 'rtl' : 'ltr' }
    );
  }

  private async loadLocal(): Promise<void> {
    const day = this.date();
    const all = await this.sync.receipts();
    this.localReceipts.set(all.filter((r) => toLocalDate(new Date(r.soldAt)) === day));
  }
}

function toLocalDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
