import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface CustomerDto { id: string; code: string; nameEn: string; nameAr: string; phone?: string; isActive: boolean; }

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  template: `
  <section class="page">
    <h1>Customers</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (auth.hasPermission('Customer.Manage')) {
      <form [formGroup]="form" (ngSubmit)="submit()">
        <input formControlName="code" placeholder="Code" />
        <input formControlName="nameEn" placeholder="Name EN" />
        <input formControlName="nameAr" placeholder="Name AR" />
        <input formControlName="phone" placeholder="Phone" />
        <app-icon-button icon="save" type="submit" tone="primary" label="Save" />
      </form>
    }
    <ul>
      @for (item of items(); track item.id) {
        <li><strong>{{ item.code }}</strong> — {{ item.nameEn }} / {{ item.nameAr }}</li>
      }
    </ul>
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:56rem; margin:0 auto; }
    form { display:grid; grid-template-columns:repeat(auto-fit,minmax(9rem,1fr)); gap:.75rem; margin-bottom:1.25rem; align-items:center; }
    input { padding:.7rem .8rem; border-radius:.35rem; border:1px solid var(--border); font:inherit; background:var(--surface); color:var(--text); }
    ul { list-style:none; padding:0; } li { padding:.75rem 0; border-bottom:1px solid var(--border); }
    .error { color:#c45c4a; }
  `]
})
export class CustomersPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly items = signal<CustomerDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly form = this.fb.group({
    code: ['', Validators.required],
    nameEn: ['', Validators.required],
    nameAr: ['', Validators.required],
    phone: ['']
  });

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.http.get<ApiResponse<CustomerDto[]>>(`${environment.apiBaseUrl}/customers`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set('Unable to load customers.')
    });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.http.post(`${environment.apiBaseUrl}/customers`, { ...this.form.getRawValue(), isActive: true }).subscribe({
      next: () => { this.form.reset(); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Create failed')
    });
  }
}
