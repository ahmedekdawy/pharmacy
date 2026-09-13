import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface SupplierDto { id: string; code: string; nameEn: string; nameAr: string; phone?: string; email?: string; isActive: boolean; }

@Component({
  selector: 'app-suppliers',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  template: `
  <section class="page">
    <h1>Suppliers</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (auth.hasPermission('Supplier.Manage')) {
      <form [formGroup]="form" (ngSubmit)="submit()">
        <input formControlName="code" placeholder="Code" />
        <input formControlName="nameEn" placeholder="Name EN" />
        <input formControlName="nameAr" placeholder="Name AR" />
        <input formControlName="phone" placeholder="Phone" />
        <input formControlName="email" placeholder="Email" />
        @if (editingId()) {
          <label class="active-toggle"><input type="checkbox" formControlName="isActive" /> Active</label>
        }
        <div class="form-actions">
          <app-icon-button icon="save" type="submit" tone="primary" [label]="editingId() ? 'Update' : 'Save'" />
          @if (editingId()) { <app-icon-button icon="close" label="Cancel" (pressed)="cancelEdit()" /> }
        </div>
      </form>
    }
    <ul>
      @for (item of items(); track item.id) {
        <li>
          <div><strong>{{ item.code }}</strong> — {{ item.nameEn }} / {{ item.nameAr }}</div>
          @if (auth.hasPermission('Supplier.Manage')) {
            <div class="actions">
              <app-icon-button icon="edit" label="Edit" (pressed)="startEdit(item)" />
              <app-icon-button icon="delete" tone="danger" label="Delete" (pressed)="remove(item)" />
            </div>
          }
        </li>
      }
    </ul>
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:56rem; margin:0 auto; }
    form { display:grid; grid-template-columns:repeat(auto-fit,minmax(9rem,1fr)); gap:.75rem; margin-bottom:1.25rem; align-items:center; }
    input { padding:.7rem .8rem; border-radius:.35rem; border:1px solid var(--border); font:inherit; background:var(--surface); color:var(--text); }
    .form-actions,.actions { display:flex; gap:.45rem; align-items:center; }
    .active-toggle { display:flex; align-items:center; gap:.4rem; }
    ul { list-style:none; padding:0; }
    li { display:flex; justify-content:space-between; gap:1rem; align-items:center; padding:.75rem 0; border-bottom:1px solid var(--border); }
    .error { color:#c45c4a; }
  `]
})
export class SuppliersPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly items = signal<SupplierDto[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly form = this.fb.group({
    code: ['', Validators.required],
    nameEn: ['', Validators.required],
    nameAr: ['', Validators.required],
    phone: [''],
    email: [''],
    isActive: [true]
  });

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.http.get<ApiResponse<SupplierDto[]>>(`${environment.apiBaseUrl}/suppliers`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set('Unable to load suppliers.')
    });
  }

  startEdit(item: SupplierDto): void {
    this.editingId.set(item.id);
    this.form.patchValue({
      code: item.code, nameEn: item.nameEn, nameAr: item.nameAr,
      phone: item.phone ?? '', email: item.email ?? '', isActive: item.isActive
    });
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
      ? this.http.put(`${environment.apiBaseUrl}/suppliers/${id}`, body)
      : this.http.post(`${environment.apiBaseUrl}/suppliers`, body);
    req$.subscribe({
      next: () => { this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Save failed')
    });
  }

  remove(item: SupplierDto): void {
    if (!confirm(`Delete ${item.code}?`)) return;
    this.http.delete(`${environment.apiBaseUrl}/suppliers/${item.id}`).subscribe({
      next: () => { if (this.editingId() === item.id) this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Delete failed')
    });
  }
}
