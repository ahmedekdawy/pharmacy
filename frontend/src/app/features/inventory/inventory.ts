import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface StockItem {
  productId: string; productCode: string; productNameEn: string; batchId?: string | null;
  batchNumber?: string | null; expiryDate?: string | null; locationId: string; locationNameEn: string; quantity: number;
}
interface NearExpiry { productCode: string; productNameEn: string; batchNumber: string; expiryDate: string; availableQuantity: number; }
interface LowStock { productCode: string; productNameEn: string; availableQuantity: number; threshold: number; }
interface NamedItem { id: string; nameEn: string; }
interface ProductItem { id: string; code: string; nameEn: string; }

@Component({
  selector: 'app-inventory',
  standalone: true,
  imports: [ReactiveFormsModule, DecimalPipe, IconButton],
  templateUrl: './inventory.html',
  styleUrl: './inventory.scss'
})
export class InventoryPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);

  readonly stock = signal<StockItem[]>([]);
  readonly nearExpiry = signal<NearExpiry[]>([]);
  readonly lowStock = signal<LowStock[]>([]);
  readonly locations = signal<NamedItem[]>([]);
  readonly products = signal<ProductItem[]>([]);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    locationId: ['', Validators.required],
    productId: ['', Validators.required],
    batchNumber: ['', Validators.required],
    expiryDate: [''],
    purchasePrice: [0, [Validators.required, Validators.min(0)]],
    quantity: [1, [Validators.required, Validators.min(0.0001)]]
  });

  readonly transferForm = this.fb.group({
    fromLocationId: ['', Validators.required],
    toLocationId: ['', Validators.required],
    productId: ['', Validators.required],
    batchId: ['', Validators.required],
    quantity: [1, [Validators.required, Validators.min(0.0001)]]
  });

  readonly adjustForm = this.fb.group({
    locationId: ['', Validators.required],
    productId: ['', Validators.required],
    batchId: ['', Validators.required],
    quantityDelta: [1, [Validators.required]],
    reason: ['']
  });

  ngOnInit(): void {
    this.reload();
    this.http.get<ApiResponse<NamedItem[]>>(`${environment.apiBaseUrl}/locations`).subscribe({
      next: (res) => this.locations.set(res.data ?? [])
    });
    this.http.get<ApiResponse<{ items: ProductItem[] }>>(`${environment.apiBaseUrl}/products`).subscribe({
      next: (res) => this.products.set(res.data?.items ?? [])
    });
  }

  batchesFor(productId: string | null | undefined, locationId?: string | null): StockItem[] {
    return this.stock().filter((s) =>
      !!s.batchId &&
      s.productId === productId &&
      (!locationId || s.locationId === locationId) &&
      s.quantity > 0
    );
  }

  reload(): void {
    this.http.get<ApiResponse<StockItem[]>>(`${environment.apiBaseUrl}/inventory/stock`).subscribe({
      next: (res) => this.stock.set(res.data ?? []),
      error: () => this.error.set('Unable to load stock.')
    });
    this.http.get<ApiResponse<NearExpiry[]>>(`${environment.apiBaseUrl}/inventory/near-expiry?days=90`).subscribe({
      next: (res) => this.nearExpiry.set(res.data ?? [])
    });
    this.http.get<ApiResponse<LowStock[]>>(`${environment.apiBaseUrl}/inventory/low-stock?threshold=10`).subscribe({
      next: (res) => this.lowStock.set(res.data ?? [])
    });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const value = this.form.getRawValue();
    this.http.post(`${environment.apiBaseUrl}/inventory/opening-balance`, {
      locationId: value.locationId,
      productId: value.productId,
      batchNumber: value.batchNumber,
      expiryDate: value.expiryDate || null,
      purchasePrice: Number(value.purchasePrice),
      quantity: Number(value.quantity)
    }).subscribe({
      next: () => {
        this.form.patchValue({ batchNumber: '', quantity: 1, purchasePrice: 0, expiryDate: '' });
        this.reload();
      },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Opening balance failed')
    });
  }

  transfer(): void {
    if (this.transferForm.invalid) { this.transferForm.markAllAsTouched(); return; }
    const v = this.transferForm.getRawValue();
    this.http.post(`${environment.apiBaseUrl}/inventory/transfer`, {
      ...v,
      quantity: Number(v.quantity),
      notes: null
    }).subscribe({
      next: () => { this.transferForm.patchValue({ quantity: 1 }); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Transfer failed')
    });
  }

  adjust(): void {
    if (this.adjustForm.invalid) { this.adjustForm.markAllAsTouched(); return; }
    const v = this.adjustForm.getRawValue();
    this.http.post(`${environment.apiBaseUrl}/inventory/adjust`, {
      locationId: v.locationId,
      productId: v.productId,
      batchId: v.batchId,
      quantityDelta: Number(v.quantityDelta),
      reason: v.reason || null
    }).subscribe({
      next: () => { this.adjustForm.patchValue({ quantityDelta: 1, reason: '' }); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Adjust failed')
    });
  }
}
