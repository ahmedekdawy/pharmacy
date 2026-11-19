import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';
import { apiErrorMessage } from '../../core/http-error';
import { LocalizationService } from '../../core/services/localization.service';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface Named { id: string; nameEn: string; nameAr?: string; code?: string; }
interface PurchaseDto { id: string; number: string; supplierNameEn: string; locationNameEn: string; totalAmount: number; status: string; purchasedAt: string; }
interface ProductItem { id: string; code: string; nameEn: string; nameAr?: string; }

@Component({
  selector: 'app-purchases',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  template: `
  <section class="page">
    <h1>{{ i18n.pick('Purchases', 'المشتريات') }}</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (auth.hasPermission('Purchase.Receive')) {
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label class="field">
          <span class="field-label">{{ i18n.pick('Supplier', 'المورد') }} <span class="req">*</span></span>
          <select formControlName="supplierId">
            <option value="">{{ i18n.pick('Supplier', 'المورد') }}</option>
            @for (s of suppliers(); track s.id) { <option [value]="s.id">{{ i18n.pick(s.nameEn, s.nameAr || s.nameEn) }}</option> }
          </select>
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Location', 'الفرع') }} <span class="req">*</span></span>
          <select formControlName="locationId">
            <option value="">{{ i18n.pick('Location', 'الفرع') }}</option>
            @for (l of locations(); track l.id) { <option [value]="l.id">{{ i18n.pick(l.nameEn, l.nameAr || l.nameEn) }}</option> }
          </select>
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Product', 'الصنف') }} <span class="req">*</span></span>
          <select formControlName="productId">
            <option value="">{{ i18n.pick('Product', 'الصنف') }}</option>
            @for (p of products(); track p.id) { <option [value]="p.id">{{ p.code }} — {{ i18n.pick(p.nameEn, p.nameAr || p.nameEn) }}</option> }
          </select>
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Batch', 'التشغيلة') }} <span class="req">*</span></span>
          <input formControlName="batchNumber" dir="ltr" [placeholder]="i18n.pick('Batch', 'التشغيلة')" />
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Expiry date', 'تاريخ الصلاحية') }}</span>
          <input formControlName="expiryDate" type="date" />
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Quantity', 'الكمية') }} <span class="req">*</span></span>
          <input formControlName="quantity" type="number" step="0.01" [placeholder]="i18n.pick('Qty', 'الكمية')" />
        </label>
        <label class="field">
          <span class="field-label">{{ i18n.pick('Unit cost', 'تكلفة الوحدة') }} <span class="req">*</span></span>
          <input formControlName="unitCost" type="number" step="0.01" [placeholder]="i18n.pick('Unit cost', 'تكلفة الوحدة')" />
        </label>
        <div class="form-actions">
          <app-icon-button icon="save" [showLabel]="true" type="submit" tone="primary" [label]="i18n.pick('Receive purchase', 'استلام المشتريات')" />
        </div>
      </form>
    }
    <ul>
      @for (item of items(); track item.id) {
        <li><strong>{{ item.number }}</strong> — {{ item.supplierNameEn }} &#64; {{ item.locationNameEn }} = {{ item.totalAmount }}</li>
      }
    </ul>
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:64rem; margin:0 auto; }
    form { display:grid; grid-template-columns:repeat(auto-fit,minmax(9rem,1fr)); gap:.75rem; margin-bottom:1.25rem; align-items:end; }
    input,select { padding:.7rem .8rem; border-radius:.35rem; border:1px solid var(--border); font:inherit; background:var(--surface); color:var(--text); }
    ul { list-style:none; padding:0; } li { padding:.75rem 0; border-bottom:1px solid var(--border); }
    .error { color:#c45c4a; }
    .form-actions { grid-column:1 / -1; display:flex; gap:.45rem; }
  `]
})
export class PurchasesPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);
  readonly items = signal<PurchaseDto[]>([]);
  readonly suppliers = signal<Named[]>([]);
  readonly locations = signal<Named[]>([]);
  readonly products = signal<ProductItem[]>([]);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    supplierId: ['', Validators.required],
    locationId: ['', Validators.required],
    productId: ['', Validators.required],
    batchNumber: ['', Validators.required],
    expiryDate: [''],
    quantity: [1, [Validators.required, Validators.min(0.0001)]],
    unitCost: [0, [Validators.required, Validators.min(0)]]
  });

  ngOnInit(): void {
    this.reload();
    this.http.get<ApiResponse<Named[]>>(`${environment.apiBaseUrl}/suppliers`).subscribe({ next: (r) => this.suppliers.set(r.data ?? []) });
    this.http.get<ApiResponse<Named[]>>(`${environment.apiBaseUrl}/locations`).subscribe({ next: (r) => this.locations.set(r.data ?? []) });
    this.http.get<ApiResponse<{ items: ProductItem[] }>>(`${environment.apiBaseUrl}/products`).subscribe({ next: (r) => this.products.set(r.data?.items ?? []) });
  }

  reload(): void {
    this.http.get<ApiResponse<PurchaseDto[]>>(`${environment.apiBaseUrl}/purchases`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set(this.i18n.pick('Unable to load purchases.', 'تعذر تحميل المشتريات.'))
    });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    this.http.post(`${environment.apiBaseUrl}/purchases/receive`, {
      supplierId: v.supplierId,
      locationId: v.locationId,
      notes: null,
      items: [{
        productId: v.productId,
        batchNumber: v.batchNumber,
        expiryDate: v.expiryDate || null,
        quantity: Number(v.quantity),
        unitCost: Number(v.unitCost)
      }]
    }).subscribe({
      next: () => { this.form.patchValue({ batchNumber: '', quantity: 1, unitCost: 0, expiryDate: '' }); this.reload(); },
      error: (err) => this.error.set(apiErrorMessage(err, this.i18n.pick('Receive failed', 'فشل الاستلام')))
    });
  }
}
