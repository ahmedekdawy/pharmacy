import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';
import { apiErrorMessage } from '../../core/http-error';
import { LocalizationService } from '../../core/services/localization.service';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface CustomerDto { id: string; code: string; nameEn: string; nameAr: string; phone?: string; address?: string | null; creditLimit: number; balance: number; isActive: boolean; }

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  template: `
  <section class="page">
    <h1>{{ i18n.pick('Customers', 'العملاء') }}</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (auth.hasPermission('Customer.Manage')) {
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label class="field">
          <span class="field-label">{{ i18n.pick('Code', 'الكود') }} <span class="req">*</span></span>
          <input formControlName="code" dir="ltr" [placeholder]="i18n.pick('Code', 'الكود')" />
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Name (English)', 'الاسم (إنجليزي)') }} <span class="req">*</span></span>
          <input formControlName="nameEn" dir="ltr" [placeholder]="i18n.pick('Name in English', 'الاسم بالإنجليزية')" />
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Name (Arabic)', 'الاسم (عربي)') }} <span class="req">*</span></span>
          <input formControlName="nameAr" [placeholder]="i18n.pick('Name in Arabic', 'الاسم بالعربية')" />
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Phone', 'الهاتف') }}</span>
          <input formControlName="phone" [placeholder]="i18n.pick('Phone', 'الهاتف')" />
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Address', 'العنوان') }}</span>
          <input formControlName="address" [placeholder]="i18n.pick('Address', 'العنوان')" />
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Credit limit (0 = no limit)', 'حد الائتمان (0 = بدون حد)') }}</span>
          <input formControlName="creditLimit" type="number" min="0" step="1" />
        </label>
        @if (editingId()) {
          <label class="active-toggle"><input type="checkbox" formControlName="isActive" /> {{ i18n.pick('Active', 'نشط') }}</label>
        }
        <div class="form-actions">
          <app-icon-button icon="save" [showLabel]="true" type="submit" tone="primary" [label]="editingId() ? i18n.pick('Update', 'تحديث') : i18n.pick('Save', 'حفظ')" />
          @if (editingId()) { <app-icon-button icon="close" [label]="i18n.pick('Cancel', 'إلغاء')" (pressed)="cancelEdit()" /> }
        </div>
      </form>
    }
    <ul>
      @for (item of items(); track item.id) {
        <li>
          <div><strong>{{ item.code }}</strong> — {{ item.nameEn }} / {{ item.nameAr }} @if (item.balance) { <span class="bal" [class.debt]="item.balance > 0">{{ item.balance.toFixed(2) }}</span> }</div>
          @if (auth.hasPermission('Customer.Manage')) {
            <div class="actions">
              <app-icon-button icon="edit" [label]="i18n.pick('Edit', 'تعديل')" (pressed)="startEdit(item)" />
              <app-icon-button icon="delete" tone="danger" [label]="i18n.pick('Delete', 'حذف')" (pressed)="remove(item)" />
            </div>
          }
        </li>
      }
    </ul>
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:56rem; margin:0 auto; }
    form { display:grid; grid-template-columns:repeat(auto-fit,minmax(9rem,1fr)); gap:.75rem; margin-bottom:1.25rem; align-items:end; }
    input { padding:.7rem .8rem; border-radius:.35rem; border:1px solid var(--border); font:inherit; background:var(--surface); color:var(--text); }
    .form-actions,.actions { display:flex; gap:.45rem; align-items:center; }
    .active-toggle { display:flex; align-items:center; gap:.4rem; }
    ul { list-style:none; padding:0; }
    li { display:flex; justify-content:space-between; gap:1rem; align-items:center; padding:.75rem 0; border-bottom:1px solid var(--border); }
    .error { color:#c45c4a; }
    .bal { margin-inline-start:.5rem; font-size:.85rem; padding:0 .45rem; border-radius:999px; background:var(--surface-muted); } .bal.debt { color:#b5462f; }
  `]
})
export class CustomersPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly items = signal<CustomerDto[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly form = this.fb.group({
    code: ['', Validators.required],
    nameEn: ['', Validators.required],
    nameAr: ['', Validators.required],
    phone: [""],
    address: [""],
    creditLimit: [0, [Validators.min(0)]],
    isActive: [true]
  });

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.http.get<ApiResponse<CustomerDto[]>>(`${environment.apiBaseUrl}/customers`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set(this.i18n.pick('Unable to load customers.', 'تعذر تحميل العملاء.'))
    });
  }

  startEdit(item: CustomerDto): void {
    this.editingId.set(item.id);
    this.form.patchValue({
      code: item.code, nameEn: item.nameEn, nameAr: item.nameAr,
      phone: item.phone ?? '', isActive: item.isActive,
      address: item.address ?? '', creditLimit: item.creditLimit ?? 0
    });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({ isActive: true, creditLimit: 0 });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const body = { ...this.form.getRawValue(), isActive: !!this.form.value.isActive, creditLimit: Number(this.form.value.creditLimit) || 0 };
    const id = this.editingId();
    const req$ = id
      ? this.http.put(`${environment.apiBaseUrl}/customers/${id}`, body)
      : this.http.post(`${environment.apiBaseUrl}/customers`, body);
    req$.subscribe({
      next: () => { this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(apiErrorMessage(err, this.i18n.pick('Save failed', 'فشل الحفظ')))
    });
  }

  remove(item: CustomerDto): void {
    if (!confirm(this.i18n.pick(`Delete ${item.code}?`, `حذف ${item.code}؟`))) return;
    this.http.delete(`${environment.apiBaseUrl}/customers/${item.id}`).subscribe({
      next: () => { if (this.editingId() === item.id) this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(apiErrorMessage(err, this.i18n.pick('Delete failed', 'فشل الحذف')))
    });
  }
}
