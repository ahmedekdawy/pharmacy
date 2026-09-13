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

  createProduct(body: {
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
    body: {
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

  deleteProduct(id: string): Observable<ApiResponse<{ id: string }>> {
    return this.http.delete<ApiResponse<{ id: string }>>(`${this.baseUrl}/products/${id}`);
  }
}
