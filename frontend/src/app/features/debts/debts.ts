import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { SyncService } from '../../core/offline/sync.service';
import { PaymentMethod, PosCustomer, newId, round2 } from '../../core/offline/models';
import { escapeHtml, money, printHtml } from '../../core/offline/print';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface StatementLine { date: string; type: 'Sale' | 'Payment' | 'Return'; reference: string; debit: number; credit: number; balance: number; notes?: string | null; }
interface Statement { customerId: string; code: string; nameEn: string; nameAr: string; phone?: string | null; creditLimit: number; balance: number; lines: StatementLine[]; }

@Component({
  selector: 'app-debts',
  standalone: true,
  imports: [DecimalPipe, DatePipe],
  template: `
  <section class="page">
    <header class="head">
      <h1>{{ pick('Customer debts', 'مديونيات العملاء') }}</h1>
      <div class="total">{{ pick('Total outstanding', 'إجمالي المديونيات') }}: <b>{{ totalDebt() | number: '1.2-2' }}</b></div>
    </header>

    <input class="search" type="search" [value]="query()" (input)="query.set($any($event.target).value)"
      [placeholder]="pick('Search name, code or phone', 'ابحث بالاسم أو الكود أو الهاتف')" />
    <label class="toggle"><input type="checkbox" [checked]="showAll()" (change)="showAll.set($any($event.target).checked)" /> {{ pick('Show all customers', 'عرض كل العملاء') }}</label>

    @if (message()) {
      <p class="ok">
        {{ message() }}
        @if (lastPayment(); as p) {
          <button type="button" class="btn" (click)="printPaymentReceipt(p.c, p.number, p.amount)">{{ pick('Print receipt', 'طباعة الإيصال') }}</button>
        }
      </p>
    }
    @if (error()) { <p class="error">{{ error() }}</p> }

    <ul class="list">
      @for (c of rows(); track c.id) {
        <li>
          <div class="who">
            <strong>{{ name(c) }}</strong>
            <small>{{ c.code }} @if (c.phone) { · <a [href]="'tel:' + c.phone">{{ c.phone }}</a> }</small>
            @if (c.creditLimit) { <small>{{ pick('Limit', 'الحد') }}: {{ c.creditLimit | number: '1.0-2' }}</small> }
          </div>
          <b class="bal" [class.debt]="c.balance > 0">{{ c.balance | number: '1.2-2' }}</b>
          <div class="actions">
            @if (auth.hasPermission('Customer.Payment') && c.balance > 0) {
              <button type="button" class="btn primary" (click)="startPayment(c)">{{ pick('Collect', 'تحصيل') }}</button>
            }
            <button type="button" class="btn" (click)="openStatement(c)" [disabled]="!sync.online()">{{ pick('Statement', 'كشف حساب') }}</button>
          </div>

          @if (paying()?.id === c.id) {
            <form class="pay" (submit)="$event.preventDefault(); collect(c)">
              <input type="number" min="0.01" step="0.01" inputmode="decimal" [value]="amount()" (input)="amount.set(+$any($event.target).value)" required />
              <select [value]="method()" (change)="method.set($any($event.target).value)">
                <option value="Cash">{{ pick('Cash', 'نقدي') }}</option>
                <option value="Card">{{ pick('Card', 'فيزا') }}</option>
                <option value="Wallet">{{ pick('Wallet', 'محفظة') }}</option>
              </select>
              <input [value]="notes()" (input)="notes.set($any($event.target).value)" [placeholder]="pick('Notes', 'ملاحظات')" />
              <button type="submit" class="btn primary" [disabled]="busy()">{{ pick('Save', 'حفظ') }}</button>
              <button type="button" class="btn" (click)="paying.set(null)">{{ pick('Cancel', 'إلغاء') }}</button>
            </form>
          }
        </li>
      } @empty {
        <li class="hint">{{ pick('No customers with outstanding balance.', 'لا توجد مديونيات.') }}</li>
      }
    </ul>

    @if (statement(); as s) {
      <div class="modal" role="dialog" aria-modal="true">
        <div class="sheet">
          <header class="head">
            <h2>{{ pick('Account statement', 'كشف حساب') }} — {{ lang() === 'ar' ? s.nameAr : s.nameEn }}</h2>
            <button type="button" class="btn" (click)="printStatement(s)">{{ pick('Print', 'طباعة') }}</button>
            <button type="button" class="btn" (click)="statement.set(null)">×</button>
          </header>
          <div class="table-wrap">
            <table>
              <thead><tr>
                <th>{{ pick('Date', 'التاريخ') }}</th><th>{{ pick('Type', 'النوع') }}</th><th>{{ pick('Ref', 'المرجع') }}</th>
                <th class="num">{{ pick('Debit', 'مدين') }}</th><th class="num">{{ pick('Credit', 'دائن') }}</th><th class="num">{{ pick('Balance', 'الرصيد') }}</th>
              </tr></thead>
              <tbody>
                @for (l of s.lines; track $index) {
                  <tr>
                    <td>{{ l.date | date: 'yyyy-MM-dd HH:mm' }}</td>
                    <td>{{ typeName(l.type) }}</td>
                    <td>{{ l.reference }}</td>
                    <td class="num">{{ l.debit ? (l.debit | number: '1.2-2') : '' }}</td>
                    <td class="num">{{ l.credit ? (l.credit | number: '1.2-2') : '' }}</td>
                    <td class="num">{{ l.balance | number: '1.2-2' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <p class="total">{{ pick('Balance', 'الرصيد') }}: <b>{{ s.balance | number: '1.2-2' }}</b></p>
        </div>
      </div>
    }
  </section>`,
  styles: [`
    :host { display:block; padding:1rem; color:var(--text); }
    .page { max-width:60rem; margin:0 auto; display:grid; gap:.75rem; }
    .head { display:flex; flex-wrap:wrap; gap:.75rem; align-items:center; justify-content:space-between; }
    h1, h2 { margin:0; }
    .total b { font-size:1.2rem; color:#b5462f; }
    input, select { padding:.6rem .7rem; border:1px solid var(--border); border-radius:.45rem; background:var(--surface); color:var(--text); font:inherit; min-width:0; }
    .search { width:100%; }
    .toggle { display:flex; gap:.4rem; align-items:center; font-size:.9rem; color:var(--muted); }
    .list { list-style:none; padding:0; margin:0; display:grid; gap:.5rem; }
    .list li { display:grid; grid-template-columns:1fr auto auto; gap:.6rem; align-items:center; background:var(--surface); border:1px solid var(--border); border-radius:.6rem; padding:.7rem; }
    .who { display:flex; flex-direction:column; gap:.15rem; small { color:var(--muted); } a { color:inherit; } }
    .bal { font-size:1.1rem; &.debt { color:#b5462f; } }
    .actions { display:flex; gap:.4rem; }
    .pay { grid-column:1 / -1; display:grid; grid-template-columns:repeat(auto-fit,minmax(7rem,1fr)); gap:.4rem; }
    .btn { border:1px solid var(--border); background:var(--surface-muted); color:var(--text); border-radius:.45rem; padding:.5rem .8rem; font:inherit; cursor:pointer; }
    .btn.primary { background:var(--accent); color:#fff; border-color:transparent; }
    .btn:disabled { opacity:.5; }
    .hint { color:var(--muted); }
    .ok { color:var(--accent-strong); } .error { color:#c45c4a; }
    .modal { position:fixed; inset:0; background:rgba(0,0,0,.45); display:grid; place-items:center; padding:1rem; z-index:50; }
    .sheet { background:var(--surface); border-radius:.8rem; padding:1rem; width:min(56rem,100%); max-height:90vh; overflow:auto; display:grid; gap:.75rem; }
    .table-wrap { overflow-x:auto; }
    table { width:100%; border-collapse:collapse; font-size:.9rem; }
    th, td { padding:.45rem; border-bottom:1px solid var(--border); text-align:start; white-space:nowrap; }
    .num { text-align:end; }
    @media (max-width: 640px) { .list li { grid-template-columns:1fr auto; } .actions { grid-column:1 / -1; } }
  `]
})
export class DebtsPage implements OnInit {
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly sync = inject(SyncService);
  private readonly http = inject(HttpClient);

  readonly query = signal('');
  readonly showAll = signal(false);
  readonly paying = signal<PosCustomer | null>(null);
  readonly amount = signal(0);
  readonly method = signal<PaymentMethod>('Cash');
  readonly notes = signal('');
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly statement = signal<Statement | null>(null);
  readonly lastPayment = signal<{ c: PosCustomer; number: string; amount: number } | null>(null);
  readonly lang = computed(() => this.i18n.language());

  readonly rows = computed(() => {
    const term = this.query().trim().toLowerCase();
    return this.sync
      .customers()
      .filter((c) => this.showAll() || c.balance > 0)
      .filter((c) => !term || c.nameEn.toLowerCase().includes(term) || c.nameAr.includes(term) || c.code.toLowerCase().includes(term) || (c.phone ?? '').includes(term))
      .sort((a, b) => b.balance - a.balance);
  });

  readonly totalDebt = computed(() => round2(this.sync.customers().reduce((s, c) => s + Math.max(0, c.balance), 0)));

  ngOnInit(): void {
    void this.sync.init().then(() => this.sync.sync());
  }

  pick(en: string, ar: string): string {
    return this.i18n.pick(en, ar);
  }

  name(c: PosCustomer): string {
    return this.lang() === 'ar' ? c.nameAr || c.nameEn : c.nameEn || c.nameAr;
  }

  typeName(type: StatementLine['type']): string {
    const map = { Sale: this.pick('Credit sale', 'بيع آجل'), Payment: this.pick('Payment', 'سداد'), Return: this.pick('Return', 'مرتجع') };
    return map[type] ?? type;
  }

  startPayment(c: PosCustomer): void {
    this.paying.set(c);
    this.amount.set(c.balance);
    this.method.set('Cash');
    this.notes.set('');
    this.message.set(null);
    this.error.set(null);
  }

  async collect(c: PosCustomer): Promise<void> {
    const amount = round2(Number(this.amount()));
    if (!(amount > 0)) return;
    this.busy.set(true);
    try {
      const id = newId();
      const number = this.sync.nextInvoiceNumber('R');
      const outcome = await this.sync.submit(
        'customerPayment.create',
        {
          id,
          number,
          customerId: c.id,
          amount,
          paymentMethod: this.method(),
          locationId: localStorage.getItem('pharmacy.pos_location') || null,
          notes: this.notes().trim() || null,
          paidAt: new Date().toISOString()
        },
        `${this.pick('Payment', 'سداد')} ${this.name(c)} — ${amount}`
      );
      if (outcome.kind === 'rejected') {
        this.error.set(outcome.error);
        return;
      }
      this.paying.set(null);
      this.message.set(
        outcome.kind === 'queued'
          ? this.pick('Payment saved on this device; it will sync automatically.', 'تم حفظ السداد على الجهاز وسيتم رفعه تلقائياً.')
          : this.pick('Payment recorded.', 'تم تسجيل السداد.')
      );
      this.lastPayment.set({ c, number, amount });
    } finally {
      this.busy.set(false);
    }
  }

  openStatement(c: PosCustomer): void {
    this.error.set(null);
    this.http.get<ApiResponse<Statement>>(`${environment.apiBaseUrl}/customers/${c.id}/statement`).subscribe({
      next: (res) => this.statement.set(res.data),
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? this.pick('Unable to load statement.', 'تعذر تحميل كشف الحساب.'))
    });
  }

  printStatement(s: Statement): void {
    const L = (en: string, ar: string) => this.pick(en, ar);
    const rows = s.lines
      .map(
        (l) => `<tr><td>${new Date(l.date).toLocaleDateString()}</td><td>${escapeHtml(this.typeName(l.type))}</td><td>${escapeHtml(l.reference)}</td>
        <td class="num">${l.debit ? money(l.debit) : ''}</td><td class="num">${l.credit ? money(l.credit) : ''}</td><td class="num">${money(l.balance)}</td></tr>`
      )
      .join('');
    printHtml(
      `<h1>${L('Account statement', 'كشف حساب')}</h1>
       <div class="center">${escapeHtml(this.lang() === 'ar' ? s.nameAr : s.nameEn)} — ${escapeHtml(s.code)} ${escapeHtml(s.phone ?? '')}</div><hr>
       <table><thead><tr><th>${L('Date', 'التاريخ')}</th><th>${L('Type', 'النوع')}</th><th>${L('Ref', 'المرجع')}</th>
       <th class="num">${L('Debit', 'مدين')}</th><th class="num">${L('Credit', 'دائن')}</th><th class="num">${L('Balance', 'الرصيد')}</th></tr></thead>
       <tbody>${rows}</tbody></table><hr><h2>${L('Balance', 'الرصيد')}: ${money(s.balance)}</h2>`,
      { title: s.code, dir: this.lang() === 'ar' ? 'rtl' : 'ltr', paperWidth: 'A4' }
    );
  }

  printPaymentReceipt(c: PosCustomer, number: string, amount: number): void {
    const after = round2(c.balance - amount);
    const L = (en: string, ar: string) => this.pick(en, ar);
    printHtml(
      `<h1>${escapeHtml(this.auth.user()?.tenantCode ?? '')}</h1>
       <div class="center"><b>${L('Payment receipt', 'إيصال سداد')}</b></div><hr>
       <div>${L('No.', 'رقم')}: ${escapeHtml(number)}</div>
       <div>${new Date().toLocaleString(this.lang() === 'ar' ? 'ar-EG' : 'en-GB')}</div>
       <div>${L('Customer', 'العميل')}: ${escapeHtml(this.name(c))}</div><hr>
       <table class="totals">
         <tr><td>${L('Previous balance', 'الرصيد السابق')}</td><td class="num">${money(c.balance)}</td></tr>
         <tr class="grand"><td>${L('Paid', 'المدفوع')}</td><td class="num">${money(amount)}</td></tr>
         <tr><td>${L('Remaining', 'المتبقي')}</td><td class="num">${money(after)}</td></tr>
       </table>`,
      { title: number, dir: this.lang() === 'ar' ? 'rtl' : 'ltr' }
    );
  }
}
