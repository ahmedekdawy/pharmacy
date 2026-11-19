import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { IconButton } from '../../shared/ui/icon-button';
import { apiErrorMessage } from '../../core/http-error';
import { LocalizationService } from '../../core/services/localization.service';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface SettingDto { key: string; value: string; }

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  template: `
  <section class="page">
    <h1>{{ i18n.pick('Settings', 'الإعدادات') }}</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    <form [formGroup]="form" (ngSubmit)="save()">
      <label class="field">
        <span class="field-label">{{ i18n.pick('Key', 'المفتاح') }} <span class="req">*</span></span>
        <input formControlName="key" dir="ltr" list="knownSettingKeys" [placeholder]="i18n.pick('Choose or type a key', 'اختر أو اكتب المفتاح')" [readonly]="!!editingKey()" />
        <datalist id="knownSettingKeys">
          @for (k of knownKeys; track k.key) { <option [value]="k.key">{{ i18n.pick(k.en, k.ar) }}</option> }
        </datalist>
        @if (labelOf(form.controls.key.value); as l) { <small class="hint">{{ l }}</small> }
      </label>
      <label class="field">
        <span class="field-label">{{ i18n.pick('Value', 'القيمة') }} <span class="req">*</span></span>
        <input formControlName="value" [placeholder]="i18n.pick('Value', 'القيمة')" />
      </label>
      <div class="form-actions">
        <app-icon-button icon="save" [showLabel]="true" type="submit" tone="primary" [label]="editingKey() ? i18n.pick('Update', 'تحديث') : i18n.pick('Save setting', 'حفظ الإعداد')" />
        @if (editingKey()) { <app-icon-button icon="close" [label]="i18n.pick('Cancel', 'إلغاء')" (pressed)="cancelEdit()" /> }
      </div>
    </form>
    <ul>
      @for (item of items(); track item.key) {
        <li>
          <div>
            <strong>{{ labelOf(item.key) || item.key }}</strong>
            <span class="value" dir="auto">{{ item.value }}</span>
            @if (labelOf(item.key)) { <small class="hint" dir="ltr">{{ item.key }}</small> }
          </div>
          <div class="actions">
            <app-icon-button icon="edit" [label]="i18n.pick('Edit', 'تعديل')" (pressed)="startEdit(item)" />
            <app-icon-button icon="delete" tone="danger" [label]="i18n.pick('Delete', 'حذف')" (pressed)="remove(item)" />
          </div>
        </li>
      } @empty {
        <li class="empty-state">{{ i18n.pick('No settings yet.', 'لا توجد إعدادات بعد.') }}</li>
      }
    </ul>
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:48rem; margin:0 auto; }
    form { display:grid; grid-template-columns:repeat(auto-fit,minmax(10rem,1fr)); gap:.75rem; margin-bottom:1.25rem; align-items:end; }
    input { padding:.7rem .8rem; border-radius:.35rem; border:1px solid var(--border); font:inherit; background:var(--surface); color:var(--text); }
    .form-actions,.actions { display:flex; gap:.45rem; align-items:center; }
    ul { list-style:none; padding:0; }
    li { display:flex; justify-content:space-between; gap:1rem; align-items:center; padding:.75rem 0; border-bottom:1px solid var(--border); }
    .error { color:#c45c4a; }
    li > div:first-child { display:grid; gap:.15rem; }
    .value { word-break:break-word; }
    .hint { color:var(--muted); font-size:.78rem; }
  `]
})
export class SettingsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly i18n = inject(LocalizationService);
  readonly items = signal<SettingDto[]>([]);
  readonly editingKey = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  /** Settings the app actually reads, with human labels. Other keys are still allowed. */
  readonly knownKeys = [
    { key: 'Contact.WhatsApp', en: 'WhatsApp number (shown on the public page)', ar: 'رقم واتساب (يظهر في صفحة الصيدلية)' }
  ];

  labelOf(key: string | null | undefined): string | null {
    const known = this.knownKeys.find((k) => k.key.toLowerCase() === (key ?? '').trim().toLowerCase());
    return known ? this.i18n.pick(known.en, known.ar) : null;
  }

  readonly form = this.fb.group({
    key: ['', Validators.required],
    value: ['', Validators.required]
  });

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.http.get<ApiResponse<SettingDto[]>>(`${environment.apiBaseUrl}/settings`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set(this.i18n.pick('Unable to load settings.', 'تعذر تحميل الإعدادات.'))
    });
  }

  startEdit(item: SettingDto): void {
    this.editingKey.set(item.key);
    this.form.patchValue({ key: item.key, value: item.value });
  }

  cancelEdit(): void {
    this.editingKey.set(null);
    this.form.reset();
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.http.put(`${environment.apiBaseUrl}/settings`, this.form.getRawValue()).subscribe({
      next: () => { this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(apiErrorMessage(err, this.i18n.pick('Save failed', 'فشل الحفظ')))
    });
  }

  remove(item: SettingDto): void {
    if (!confirm(this.i18n.pick(`Delete setting ${item.key}?`, `حذف الإعداد ${item.key}؟`))) return;
    this.http.delete(`${environment.apiBaseUrl}/settings/${encodeURIComponent(item.key)}`).subscribe({
      next: () => { if (this.editingKey() === item.key) this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(apiErrorMessage(err, this.i18n.pick('Delete failed', 'فشل الحذف')))
    });
  }
}
