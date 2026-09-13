import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface NamedDto { id: string; nameEn: string; nameAr: string; isActive: boolean; }

@Component({
  selector: 'app-brands',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  templateUrl: './brands.html',
  styleUrl: './brands.scss'
})
export class BrandsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly items = signal<NamedDto[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly form = this.fb.group({
    nameEn: ['', Validators.required],
    nameAr: ['', Validators.required],
    isActive: [true]
  });

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.http.get<ApiResponse<NamedDto[]>>(`${environment.apiBaseUrl}/brands`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set('Unable to load brands.')
    });
  }

  startEdit(item: NamedDto): void {
    this.editingId.set(item.id);
    this.form.patchValue({ nameEn: item.nameEn, nameAr: item.nameAr, isActive: item.isActive });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({ isActive: true });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const body = { ...this.form.getRawValue(), isActive: !!this.form.value.isActive };
    const id = this.editingId();
    const req$ = id
      ? this.http.put(`${environment.apiBaseUrl}/brands/${id}`, body)
      : this.http.post(`${environment.apiBaseUrl}/brands`, body);
    req$.subscribe({
      next: () => { this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Save failed')
    });
  }

  remove(item: NamedDto): void {
    if (!confirm(`Delete ${item.nameEn}?`)) return;
    this.http.delete(`${environment.apiBaseUrl}/brands/${item.id}`).subscribe({
      next: () => { if (this.editingId() === item.id) this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Delete failed')
    });
  }
}
