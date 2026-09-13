import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, Subscription, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, ApiService, ProductDto } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { IconButton } from '../../shared/ui/icon-button';

export interface EgyptianDrug {
  commercialNameEn: string;
  commercialNameAr: string;
  scientificName: string;
  manufacturer: string;
  drugClass: string;
  route: string;
  priceEgp: number;
}

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [ReactiveFormsModule, DecimalPipe, IconButton],
  templateUrl: './products.html',
  styleUrl: './products.scss'
})
export class Products implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  private readonly search$ = new Subject<string>();
  private searchSub?: Subscription;
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);

  readonly products = signal<ProductDto[]>([]);
  readonly catalogResults = signal<EgyptianDrug[]>([]);
  readonly selectedDrug = signal<EgyptianDrug | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly catalogLoading = signal(false);
  readonly catalogOpen = signal(false);

  readonly catalogSearch = this.fb.nonNullable.control('');
  readonly form = this.fb.group({
    code: ['', Validators.required],
    nameAr: ['', Validators.required],
    nameEn: ['', Validators.required],
    barcode: [''],
    sellingPrice: [0, [Validators.required, Validators.min(0)]],
    isActive: [true]
  });

  ngOnInit(): void {
    this.reload();
    this.searchSub = this.search$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => {
          this.catalogLoading.set(true);
          return this.http.get<ApiResponse<EgyptianDrug[]>>(
            `${environment.apiBaseUrl}/catalog/egyptian-drugs`,
            { params: { search: term, limit: 20 } }
          );
        })
      )
      .subscribe({
        next: (res) => {
          this.catalogResults.set(res.data ?? []);
          this.catalogOpen.set(true);
          this.catalogLoading.set(false);
        },
        error: () => {
          this.catalogLoading.set(false);
          this.error.set('Unable to search Egyptian drug catalog.');
        }
      });
  }

  ngOnDestroy(): void {
    this.searchSub?.unsubscribe();
  }

  t(key: string): string {
    return this.i18n.translate(key);
  }

  onCatalogInput(value: string): void {
    this.catalogSearch.setValue(value);
    if (!value.trim()) {
      this.catalogResults.set([]);
      this.catalogOpen.set(false);
      return;
    }
    this.search$.next(value.trim());
  }

  selectDrug(drug: EgyptianDrug): void {
    this.editingId.set(null);
    this.selectedDrug.set(drug);
    this.catalogOpen.set(false);
    this.catalogSearch.setValue(drug.commercialNameEn || drug.commercialNameAr);
    this.form.patchValue({
      code: buildProductCode(drug),
      nameEn: drug.commercialNameEn || drug.scientificName,
      nameAr: drug.commercialNameAr || drug.commercialNameEn,
      barcode: '',
      sellingPrice: Number(drug.priceEgp) || 0,
      isActive: true
    });
    this.form.markAsDirty();
  }

  startEdit(product: ProductDto): void {
    this.selectedDrug.set(null);
    this.catalogOpen.set(false);
    this.editingId.set(product.id);
    this.form.patchValue({
      code: product.code,
      nameEn: product.nameEn,
      nameAr: product.nameAr,
      barcode: product.barcode ?? '',
      sellingPrice: product.sellingPrice,
      isActive: product.isActive
    });
  }

  cancelEdit(): void {
    this.clearForm();
  }

  clearForm(): void {
    this.editingId.set(null);
    this.selectedDrug.set(null);
    this.catalogSearch.setValue('');
    this.catalogResults.set([]);
    this.catalogOpen.set(false);
    this.form.reset({ sellingPrice: 0, isActive: true });
  }

  reload(): void {
    this.loading.set(true);
    this.api.searchProducts('', 1, 100).subscribe({
      next: (res) => {
        this.products.set(res.data?.items ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load products. Ensure API is running and tenant header is set.');
        this.loading.set(false);
      }
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const editingId = this.editingId();
    const payload = {
      code: value.code!,
      nameAr: value.nameAr!,
      nameEn: value.nameEn!,
      barcode: value.barcode || undefined,
      sellingPrice: Number(value.sellingPrice),
      isActive: !!value.isActive
    };

    if (editingId) {
      this.api.updateProduct(editingId, payload).subscribe({
        next: () => {
          this.clearForm();
          this.reload();
        },
        error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Update product failed.')
      });
      return;
    }

    this.api.createProduct(payload).subscribe({
      next: () => {
        this.clearForm();
        this.reload();
      },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Create product failed.')
    });
  }

  remove(product: ProductDto): void {
    if (!confirm(`Delete product ${product.code}?`)) {
      return;
    }

    this.api.deleteProduct(product.id).subscribe({
      next: () => {
        if (this.editingId() === product.id) {
          this.clearForm();
        }
        this.reload();
      },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Delete product failed.')
    });
  }
}

function buildProductCode(drug: EgyptianDrug): string {
  const source = (drug.commercialNameEn || drug.scientificName || 'DRUG').toUpperCase();
  const slug = source.replace(/[^A-Z0-9]+/g, '').slice(0, 16) || 'DRUG';
  return `EG-${slug}`;
}
