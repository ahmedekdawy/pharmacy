import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface SaleDto { id: string; number: string; locationNameEn: string; totalAmount: number; }
interface SaleDetailItem {
  id: string; productCode: string; productNameEn: string; quantity: number;
  returnedQuantity: number; unitPrice: number;
}
interface SaleDetail { id: string; number: string; items: SaleDetailItem[]; }
interface ReturnDto { id: string; number: string; saleNumber: string; locationNameEn: string; totalAmount: number; returnedAt: string; }

@Component({
  selector: 'app-returns',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  template: `
  <section class="page">
    <h1>Sale Returns</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (auth.hasPermission('Sale.Return')) {
      <form [formGroup]="form" (ngSubmit)="loadSale()">
        <select formControlName="saleId">
          <option value="">Select sale</option>
          @for (s of sales(); track s.id) {
            <option [value]="s.id">{{ s.number }} — {{ s.totalAmount }}</option>
          }
        </select>
        <app-icon-button icon="check" type="submit" tone="primary" label="Load sale" />
      </form>

      @if (detail(); as d) {
        <div class="detail">
          <h2>{{ d.number }}</h2>
          <ul>
            @for (item of d.items; track item.id) {
              <li>
                {{ item.productCode }} — {{ item.productNameEn }}
                (sold {{ item.quantity }}, returned {{ item.returnedQuantity }})
                @if (item.quantity > item.returnedQuantity) {
                  <button type="button" class="link" (click)="returnItem(item)">Return remaining</button>
                }
              </li>
            }
          </ul>
        </div>
      }
    }
    <ul>
      @for (item of items(); track item.id) {
        <li><strong>{{ item.number }}</strong> for {{ item.saleNumber }} @ {{ item.locationNameEn }} = {{ item.totalAmount }}</li>
      }
    </ul>
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:64rem; margin:0 auto; }
    form { display:flex; gap:.75rem; flex-wrap:wrap; margin-bottom:1rem; align-items:center; }
    select { padding:.7rem .8rem; border-radius:.35rem; border:1px solid var(--border); font:inherit; background:var(--surface); color:var(--text); min-width:16rem; }
    ul { list-style:none; padding:0; } li { padding:.75rem 0; border-bottom:1px solid var(--border); }
    .link { margin-inline-start:.75rem; border:0; background:transparent; color:var(--accent-strong); cursor:pointer; font:inherit; text-decoration:underline; }
    .error { color:#c45c4a; }
  `]
})
export class ReturnsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly sales = signal<SaleDto[]>([]);
  readonly items = signal<ReturnDto[]>([]);
  readonly detail = signal<SaleDetail | null>(null);
  readonly error = signal<string | null>(null);
  readonly form = this.fb.group({ saleId: ['', Validators.required] });

  ngOnInit(): void {
    this.reload();
    this.http.get<ApiResponse<SaleDto[]>>(`${environment.apiBaseUrl}/sales`).subscribe({
      next: (r) => this.sales.set(r.data ?? [])
    });
  }

  reload(): void {
    this.http.get<ApiResponse<ReturnDto[]>>(`${environment.apiBaseUrl}/sale-returns`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set('Unable to load returns.')
    });
  }

  loadSale(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const id = this.form.getRawValue().saleId!;
    this.http.get<ApiResponse<SaleDetail>>(`${environment.apiBaseUrl}/sales/${id}`).subscribe({
      next: (res) => this.detail.set(res.data),
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Load failed')
    });
  }

  returnItem(item: SaleDetailItem): void {
    const qty = item.quantity - item.returnedQuantity;
    if (qty <= 0) return;
    const saleId = this.detail()?.id;
    if (!saleId) return;
    this.http.post(`${environment.apiBaseUrl}/sale-returns`, {
      saleId,
      notes: null,
      items: [{ saleItemId: item.id, quantity: qty }]
    }).subscribe({
      next: () => { this.loadSale(); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Return failed')
    });
  }
}
