import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';
import { LocalizationService } from '../../core/services/localization.service';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface SalesReport {
  from: string; to: string; salesCount: number; salesTotal: number;
  returnsCount: number; returnsTotal: number; expensesTotal: number; netSales: number;
}
interface ProfitReport {
  from: string; to: string; salesTotal: number; returnsTotal: number;
  costOfGoodsSold: number; expensesTotal: number; grossProfit: number; netProfit: number;
}

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  template: `
  <section class="page">
    <h1>{{ i18n.pick('Reports', 'التقارير') }}</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    <form [formGroup]="form" (ngSubmit)="load()">
      <label class="field">
        <span class="field-label">{{ i18n.pick('From date', 'من تاريخ') }}</span>
        <input formControlName="from" type="date" />
      </label>
      <label class="field">
        <span class="field-label">{{ i18n.pick('To date', 'إلى تاريخ') }}</span>
        <input formControlName="to" type="date" />
      </label>
      <app-icon-button icon="check" type="submit" tone="primary" [label]="i18n.pick('Run reports', 'عرض التقارير')" />
    </form>

    @if (sales(); as r) {
      <h2>{{ i18n.pick('Sales', 'المبيعات') }}</h2>
      <dl>
        <div><dt>{{ i18n.pick('Sales count', 'عدد الفواتير') }}</dt><dd>{{ r.salesCount }}</dd></div>
        <div><dt>{{ i18n.pick('Sales total', 'إجمالي المبيعات') }}</dt><dd>{{ r.salesTotal }}</dd></div>
        <div><dt>{{ i18n.pick('Returns total', 'إجمالي المرتجعات') }}</dt><dd>{{ r.returnsTotal }}</dd></div>
        <div><dt>{{ i18n.pick('Expenses', 'المصروفات') }}</dt><dd>{{ r.expensesTotal }}</dd></div>
        <div><dt>{{ i18n.pick('Net sales', 'صافي المبيعات') }}</dt><dd>{{ r.netSales }}</dd></div>
      </dl>
    }

    @if (auth.hasPermission('Report.Profit') && profit(); as p) {
      <h2>{{ i18n.pick('Profit', 'الربح') }}</h2>
      <dl>
        <div><dt>{{ i18n.pick('Sales total', 'إجمالي المبيعات') }}</dt><dd>{{ p.salesTotal }}</dd></div>
        <div><dt>{{ i18n.pick('Returns total', 'إجمالي المرتجعات') }}</dt><dd>{{ p.returnsTotal }}</dd></div>
        <div><dt>{{ i18n.pick('COGS', 'تكلفة البضاعة المباعة') }}</dt><dd>{{ p.costOfGoodsSold }}</dd></div>
        <div><dt>{{ i18n.pick('Gross profit', 'مجمل الربح') }}</dt><dd>{{ p.grossProfit }}</dd></div>
        <div><dt>{{ i18n.pick('Expenses', 'المصروفات') }}</dt><dd>{{ p.expensesTotal }}</dd></div>
        <div><dt>{{ i18n.pick('Net profit', 'صافي الربح') }}</dt><dd>{{ p.netProfit }}</dd></div>
      </dl>
    }
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:48rem; margin:0 auto; }
    form { display:flex; gap:.75rem; flex-wrap:wrap; margin-bottom:1.25rem; align-items:end; }
    input { padding:.7rem .8rem; border-radius:.35rem; border:1px solid var(--border); font:inherit; background:var(--surface); color:var(--text); }
    h2 { margin:1.5rem 0 .5rem; font-size:1.05rem; }
    dl { display:grid; gap:.75rem; margin:0; }
    dl div { display:flex; justify-content:space-between; gap:1rem; padding:.85rem 0; border-bottom:1px solid var(--border); }
    dt { opacity:.75; } dd { margin:0; font-weight:650; }
    .error { color:#c45c4a; }
  `]
})
export class ReportsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly sales = signal<SalesReport | null>(null);
  readonly profit = signal<ProfitReport | null>(null);
  readonly error = signal<string | null>(null);
  readonly form = this.fb.group({
    from: [toDateInput(daysAgo(7))],
    to: [toDateInput(new Date())]
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    const v = this.form.getRawValue();
    const params: Record<string, string> = {};
    if (v.from) params['from'] = new Date(v.from).toISOString();
    if (v.to) params['to'] = new Date(`${v.to}T23:59:59Z`).toISOString();

    this.http.get<ApiResponse<SalesReport>>(`${environment.apiBaseUrl}/reports/sales`, { params }).subscribe({
      next: (res) => this.sales.set(res.data),
      error: () => this.error.set(this.i18n.pick('Unable to load sales report.', 'تعذر تحميل تقرير المبيعات.'))
    });

    if (this.auth.hasPermission('Report.Profit')) {
      this.http.get<ApiResponse<ProfitReport>>(`${environment.apiBaseUrl}/reports/profit`, { params }).subscribe({
        next: (res) => this.profit.set(res.data),
        error: () => this.error.set(this.i18n.pick('Unable to load profit report.', 'تعذر تحميل تقرير الربح.'))
      });
    }
  }
}

function daysAgo(n: number): Date {
  const d = new Date();
  d.setDate(d.getDate() - n);
  return d;
}

function toDateInput(d: Date): string {
  return d.toISOString().slice(0, 10);
}
