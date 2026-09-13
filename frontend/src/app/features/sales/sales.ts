import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface Named { id: string; nameEn: string; }
interface SaleDto { id: string; number: string; locationNameEn: string; customerNameEn?: string | null; totalAmount: number; soldAt: string; }
interface ProductItem { id: string; code: string; nameEn: string; sellingPrice?: number; }

@Component({
  selector: 'app-sales',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  template: `
  <section class="page">
    <h1>Sales / POS</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (auth.hasPermission('Sale.Create')) {
      <form [formGroup]="form" (ngSubmit)="submit()">
        <select formControlName="locationId">
          <option value="">Location</option>
          @for (l of locations(); track l.id) { <option [value]="l.id">{{ l.nameEn }}</option> }
        </select>
        <select formControlName="customerId">
          <option value="">Walk-in customer</option>
          @for (c of customers(); track c.id) { <option [value]="c.id">{{ c.nameEn }}</option> }
        </select>
        <select formControlName="productId">
          <option value="">Product</option>
          @for (p of products(); track p.id) { <option [value]="p.id">{{ p.code }} — {{ p.nameEn }}</option> }
        </select>
        <input formControlName="quantity" type="number" step="0.01" placeholder="Qty" />
        <app-icon-button icon="check" type="submit" tone="primary" label="Complete sale" />
      </form>
    }
    <ul>
      @for (item of items(); track item.id) {
        <li><strong>{{ item.number }}</strong> — {{ item.locationNameEn }} / {{ item.customerNameEn || 'Walk-in' }} = {{ item.totalAmount }}</li>
      }
    </ul>
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:64rem; margin:0 auto; }
    form { display:grid; grid-template-columns:repeat(auto-fit,minmax(9rem,1fr)); gap:.75rem; margin-bottom:1.25rem; align-items:center; }
    input,select { padding:.7rem .8rem; border-radius:.35rem; border:1px solid var(--border); font:inherit; background:var(--surface); color:var(--text); }
    ul { list-style:none; padding:0; } li { padding:.75rem 0; border-bottom:1px solid var(--border); }
    .error { color:#c45c4a; }
  `]
})
export class SalesPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly items = signal<SaleDto[]>([]);
  readonly locations = signal<Named[]>([]);
  readonly customers = signal<Named[]>([]);
  readonly products = signal<ProductItem[]>([]);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    locationId: ['', Validators.required],
    customerId: [''],
    productId: ['', Validators.required],
    quantity: [1, [Validators.required, Validators.min(0.0001)]]
  });

  ngOnInit(): void {
    this.reload();
    this.http.get<ApiResponse<Named[]>>(`${environment.apiBaseUrl}/locations`).subscribe({ next: (r) => this.locations.set(r.data ?? []) });
    this.http.get<ApiResponse<Named[]>>(`${environment.apiBaseUrl}/customers`).subscribe({ next: (r) => this.customers.set(r.data ?? []) });
    this.http.get<ApiResponse<{ items: ProductItem[] }>>(`${environment.apiBaseUrl}/products`).subscribe({ next: (r) => this.products.set(r.data?.items ?? []) });
  }

  reload(): void {
    this.http.get<ApiResponse<SaleDto[]>>(`${environment.apiBaseUrl}/sales`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set('Unable to load sales.')
    });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    this.http.post(`${environment.apiBaseUrl}/sales`, {
      locationId: v.locationId,
      customerId: v.customerId || null,
      notes: null,
      items: [{ productId: v.productId, batchId: null, quantity: Number(v.quantity), unitPrice: null }]
    }).subscribe({
      next: () => { this.form.patchValue({ quantity: 1 }); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Sale failed')
    });
  }
}
