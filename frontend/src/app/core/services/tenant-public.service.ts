import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface TenantPublicInfo {
  tenantCode: string;
  nameEn: string;
  nameAr: string;
  whatsApp: string | null;
}

interface ApiResponse<T> {
  data: T;
  success: boolean;
  errors: string[];
}

@Injectable({ providedIn: 'root' })
export class TenantPublicService {
  private readonly http = inject(HttpClient);

  getPublicInfo(tenantCode: string): Observable<ApiResponse<TenantPublicInfo>> {
    return this.http.get<ApiResponse<TenantPublicInfo>>(
      `${environment.apiBaseUrl}/tenants/${tenantCode}/public-info`
    );
  }
}
