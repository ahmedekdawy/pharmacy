import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';
import { apiErrorMessage } from '../../core/http-error';
import { LocalizationService } from '../../core/services/localization.service';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface LocationDto { id: string; nameEn: string; nameAr: string; type: number | string; isActive: boolean; }

@Component({
  selector: 'app-locations',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  templateUrl: './locations.html',
  styleUrl: './locations.scss'
})
export class LocationsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly items = signal<LocationDto[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly form = this.fb.group({
    nameEn: ['', Validators.required],
    nameAr: ['', Validators.required],
    type: ['1', Validators.required],
    isActive: [true]
  });

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.http.get<ApiResponse<LocationDto[]>>(`${environment.apiBaseUrl}/locations`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set(this.i18n.pick('Unable to load locations.', 'تعذر تحميل الفروع.'))
    });
  }

  typeLabel(type: number | string): string {
    const value = Number(type);
    return value === 2 ? this.i18n.pick('Warehouse', 'مخزن') : value === 1 ? this.i18n.pick('Branch', 'فرع') : String(type);
  }

  startEdit(item: LocationDto): void {
    this.editingId.set(item.id);
    this.form.patchValue({
      nameEn: item.nameEn,
      nameAr: item.nameAr,
      type: String(Number(item.type) || 1),
      isActive: item.isActive
    });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({ type: '1', isActive: true });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const body = {
      nameEn: v.nameEn,
      nameAr: v.nameAr,
      type: Number(v.type),
      isActive: !!v.isActive
    };
    const id = this.editingId();
    const req$ = id
      ? this.http.put(`${environment.apiBaseUrl}/locations/${id}`, body)
      : this.http.post(`${environment.apiBaseUrl}/locations`, body);
    req$.subscribe({
      next: () => { this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(apiErrorMessage(err, this.i18n.pick('Save failed', 'فشل الحفظ')))
    });
  }

  remove(item: LocationDto): void {
    if (!confirm(this.i18n.pick(`Delete ${item.nameEn}?`, `حذف ${item.nameAr}؟`))) return;
    this.http.delete(`${environment.apiBaseUrl}/locations/${item.id}`).subscribe({
      next: () => { if (this.editingId() === item.id) this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(apiErrorMessage(err, this.i18n.pick('Delete failed', 'فشل الحذف')))
    });
  }
}
