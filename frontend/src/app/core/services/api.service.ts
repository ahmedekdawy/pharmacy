import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ApiResponse<T> {
  data: T;
  success: boolean;
  errors: string[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ProductDto {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  barcode?: string | null;
  categoryId?: string | null;
  sellingPrice: number;
  isActive: boolean;
  stripsPerBox?: number;
  unitsPerStrip?: number;
  allowStripSale?: boolean;
  allowUnitSale?: boolean;
  stripPrice?: number | null;
  unitPrice?: number | null;
}

export interface ProductPackFields {
  stripsPerBox?: number;
  unitsPerStrip?: number;
  allowStripSale?: boolean;
  allowUnitSale?: boolean;
  stripPrice?: number | null;
  unitPrice?: number | null;
}

export interface EgyptianDrug {
  commercialNameEn: string;
  commercialNameAr: string;
  scientificName: string;
  manufacturer: string;
  drugClass: string;
  route: string;
  priceEgp: number;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  searchProducts(search = '', page = 1, pageSize = 20): Observable<ApiResponse<PagedResult<ProductDto>>> {
    return this.http.get<ApiResponse<PagedResult<ProductDto>>>(`${this.baseUrl}/products`, {
      params: { search, page, pageSize }
    });
  }

  createProduct(body: ProductPackFields & {
    code: string;
    nameAr: string;
    nameEn: string;
    barcode?: string;
    categoryId?: string;
    sellingPrice: number;
  }): Observable<ApiResponse<{ id: string }>> {
    return this.http.post<ApiResponse<{ id: string }>>(`${this.baseUrl}/products`, body);
  }

  updateProduct(
    id: string,
    body: ProductPackFields & {
      code: string;
      nameAr: string;
      nameEn: string;
      barcode?: string;
      categoryId?: string;
      sellingPrice: number;
      isActive: boolean;
    }
  ): Observable<ApiResponse<{ id: string }>> {
    return this.http.put<ApiResponse<{ id: string }>>(`${this.baseUrl}/products/${id}`, body);
  }

  updateProductPrice(id: string, sellingPrice: number): Observable<ApiResponse<{ id: string }>> {
    return this.http.patch<ApiResponse<{ id: string }>>(`${this.baseUrl}/products/${id}/price`, { sellingPrice });
  }

  deleteProduct(id: string): Observable<ApiResponse<{ id: string }>> {
    return this.http.delete<ApiResponse<{ id: string }>>(`${this.baseUrl}/products/${id}`);
  }

  searchEgyptianDrugs(search: string, limit = 20): Observable<ApiResponse<EgyptianDrug[]>> {
    return this.http.get<ApiResponse<EgyptianDrug[]>>(`${this.baseUrl}/catalog/egyptian-drugs`, {
      params: { search, limit }
    });
  }

  /** The whole reference list, for clients that cache it locally to search offline. */
  getAllEgyptianDrugs(): Observable<ApiResponse<EgyptianDrug[]>> {
    return this.http.get<ApiResponse<EgyptianDrug[]>>(`${this.baseUrl}/catalog/egyptian-drugs/all`);
  }
}
