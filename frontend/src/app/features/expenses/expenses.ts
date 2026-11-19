import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';
import { apiErrorMessage } from '../../core/http-error';
import { LocalizationService } from '../../core/services/localization.service';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface Named { id: string; nameEn: string; }
interface ExpenseDto {
  id: string; locationId: string; locationNameEn: string; category: string;
  descriptionEn: string; descriptionAr: string; amount: number; spentAt: string;
}

@Component({
  selector: 'app-expenses',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  template: `
  <section class="page">
    <h1>{{ i18n.pick('Expenses', 'المصروفات') }}</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (auth.hasPermission('Expense.Manage')) {
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label class="field">
          <span class="field-label">{{ i18n.pick('Location', 'الفرع') }} <span class="req">*</span></span>
          <select formControlName="locationId">
            <option value="">{{ i18n.pick('Location', 'الفرع') }}</option>
            @for (l of locations(); track l.id) { <option [value]="l.id">{{ l.nameEn }}</option> }
          </select>
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Category', 'التصنيف') }} <span class="req">*</span></span>
          <input formControlName="category" [placeholder]="i18n.pick('Category', 'التصنيف')" />
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Description (English)', 'الوصف (إنجليزي)') }} <span class="req">*</span></span>
          <input formControlName="descriptionEn" [placeholder]="i18n.pick('Description EN', 'الوصف بالإنجليزية')" />
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Description (Arabic)', 'الوصف (عربي)') }} <span class="req">*</span></span>
          <input formControlName="descriptionAr" [placeholder]="i18n.pick('Description AR', 'الوصف بالعربية')" />
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Amount', 'المبلغ') }} <span class="req">*</span></span>
          <input formControlName="amount" type="number" step="0.01" [placeholder]="i18n.pick('Amount', 'المبلغ')" />
        </label>
        <div class="form-actions">
          <app-icon-button icon="save" type="submit" tone="primary" [label]="editingId() ? i18n.pick('Update', 'تحديث') : i18n.pick('Save', 'حفظ')" />
          @if (editingId()) { <app-icon-button icon="close" [label]="i18n.pick('Cancel', 'إلغاء')" (pressed)="cancelEdit()" /> }
        </div>
      </form>
    }
    <ul>
      @for (item of items(); track item.id) {
        <li>
          <div><strong>{{ item.category }}</strong> — {{ i18n.pick(item.descriptionEn, item.descriptionAr || item.descriptionEn) }} &#64; {{ item.locationNameEn }} = {{ item.amount }}</div>
          @if (auth.hasPermission('Expense.Manage')) {
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
    .page { max-width:64rem; margin:0 auto; }
    form { display:grid; grid-template-columns:repeat(auto-fit,minmax(9rem,1fr)); gap:.75rem; margin-bottom:1.25rem; align-items:end; }
    input,select { padding:.7rem .8rem; border-radius:.35rem; border:1px solid var(--border); font:inherit; background:var(--surface); color:var(--text); }
    .form-actions,.actions { display:flex; gap:.45rem; align-items:center; }
    ul { list-style:none; padding:0; }
    li { display:flex; justify-content:space-between; gap:1rem; align-items:center; padding:.75rem 0; border-bottom:1px solid var(--border); }
    .error { color:#c45c4a; }
  `]
})
export class ExpensesPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly items = signal<ExpenseDto[]>([]);
  readonly locations = signal<Named[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly form = this.fb.group({
    locationId: ['', Validators.required],
    category: ['', Validators.required],
    descriptionEn: ['', Validators.required],
    descriptionAr: ['', Validators.required],
    amount: [0, [Validators.required, Validators.min(0.01)]]
  });

  ngOnInit(): void {
    this.reload();
    this.http.get<ApiResponse<Named[]>>(`${environment.apiBaseUrl}/locations`).subscribe({
      next: (r) => this.locations.set(r.data ?? [])
    });
  }

  reload(): void {
    this.http.get<ApiResponse<ExpenseDto[]>>(`${environment.apiBaseUrl}/expenses`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set(this.i18n.pick('Unable to load expenses.', 'تعذر تحميل المصروفات.'))
    });
  }

  startEdit(item: ExpenseDto): void {
    this.editingId.set(item.id);
    this.form.patchValue({
      locationId: item.locationId,
      category: item.category,
      descriptionEn: item.descriptionEn,
      descriptionAr: item.descriptionAr,
      amount: item.amount
    });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({ amount: 0 });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const body = { ...v, amount: Number(v.amount), cashShiftId: null };
    const id = this.editingId();
    const req$ = id
      ? this.http.put(`${environment.apiBaseUrl}/expenses/${id}`, body)
      : this.http.post(`${environment.apiBaseUrl}/expenses`, body);
    req$.subscribe({
      next: () => { this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(apiErrorMessage(err, this.i18n.pick('Save failed', 'فشل الحفظ')))
    });
  }

  remove(item: ExpenseDto): void {
    if (!confirm(this.i18n.pick(`Delete expense ${item.category}?`, `حذف المصروف ${item.category}؟`))) return;
    this.http.delete(`${environment.apiBaseUrl}/expenses/${item.id}`).subscribe({
      next: () => { if (this.editingId() === item.id) this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(apiErrorMessage(err, this.i18n.pick('Delete failed', 'فشل الحذف')))
    });
  }
}
