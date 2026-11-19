import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { environment } from '../../../environments/environment';
import { LocalizationService } from '../../core/services/localization.service';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface AuditLogDto {
  id: string; userEmail?: string | null; action: string; entityType: string;
  entityId?: string | null; oldValues?: string | null; newValues?: string | null; createdAt: string;
}

const ACTIONS: Record<string, [string, string]> = {
  Create: ['Created', 'إضافة'],
  Update: ['Updated', 'تعديل'],
  Delete: ['Deleted', 'حذف'],
  TransferStock: ['Stock transfer', 'تحويل مخزون'],
  AdjustInventory: ['Stock adjustment', 'تسوية مخزون']
};

const ENTITIES: Record<string, string> = {
  Product: 'صنف', Category: 'تصنيف', Brand: 'علامة تجارية', Supplier: 'مورد', Customer: 'عميل',
  CustomerPayment: 'تحصيل من عميل', Sale: 'فاتورة بيع', SaleReturn: 'مرتجع', Purchase: 'فاتورة شراء',
  Expense: 'مصروف', CashShift: 'وردية', Location: 'فرع', User: 'مستخدم', Role: 'دور', UserRole: 'دور مستخدم',
  RolePermission: 'صلاحية دور', RolePage: 'صفحة دور', TenantSetting: 'إعداد', DrugBarcode: 'باركود',
  StockTransfer: 'تحويل مخزون', StockAdjustment: 'تسوية مخزون'
};

@Component({
  selector: 'app-audit',
  standalone: true,
  imports: [DatePipe],
  template: `
  <section class="page">
    <h1>{{ i18n.pick('Audit Logs', 'سجل التدقيق') }}</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    <ul>
      @for (item of items(); track item.id) {
        <li>
          <div>
            <strong>{{ actionLabel(item.action) }}</strong> · {{ entityLabel(item.entityType) }}
            <span>{{ item.userEmail || i18n.pick('system', 'النظام') }} · {{ item.createdAt | date:'short' }}</span>
          </div>
          @if (item.oldValues) { <pre class="old" dir="ltr">{{ item.oldValues }}</pre> }
          @if (item.newValues) { <pre dir="ltr">{{ item.newValues }}</pre> }
        </li>
      } @empty {
        @if (!loading() && !error()) {
          <li class="empty-state">{{ i18n.pick('No activity recorded yet. Changes to products, sales, users and other records appear here.', 'لا توجد عمليات مسجلة بعد. ستظهر هنا التعديلات على الأصناف والمبيعات والمستخدمين وغيرها.') }}</li>
        }
      }
    </ul>
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:64rem; margin:0 auto; }
    ul { list-style:none; padding:0; }
    li { padding:.9rem 0; border-bottom:1px solid var(--border); display:grid; gap:.35rem; }
    span { display:block; opacity:.7; font-size:.85rem; }
    pre { margin:0; white-space:pre-wrap; word-break:break-word; font-size:.8rem; opacity:.85; }
    .error { color:#c45c4a; }
    pre.old { text-decoration:line-through; opacity:.6; }
  `]
})
export class AuditPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly i18n = inject(LocalizationService);
  readonly items = signal<AuditLogDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.http.get<ApiResponse<AuditLogDto[]>>(`${environment.apiBaseUrl}/audit-logs`, { params: { take: 100 } }).subscribe({
      next: (res) => {
        this.items.set(res.data ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(this.i18n.pick('Unable to load audit logs.', 'تعذر تحميل سجل التدقيق.'));
      }
    });
  }

  actionLabel(action: string): string {
    const a = ACTIONS[action];
    return a ? this.i18n.pick(a[0], a[1]) : action;
  }

  entityLabel(entity: string): string {
    return this.i18n.language() === 'ar' ? ENTITIES[entity] ?? entity : entity;
  }
}
