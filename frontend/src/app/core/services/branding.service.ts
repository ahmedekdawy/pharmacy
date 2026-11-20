import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type BrandingKind = 'logo' | 'banner';

interface ApiResponse<T> {
  data: T;
  success: boolean;
  errors: string[];
}

@Injectable({ providedIn: 'root' })
export class BrandingService {
  private readonly http = inject(HttpClient);

  upload(kind: BrandingKind, file: File): Observable<ApiResponse<{ path: string }>> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ApiResponse<{ path: string }>>(
      `${environment.apiBaseUrl}/tenants/branding/${kind}`,
      form
    );
  }

  /** Public, cache-busted URL for an `<img>` tag; no auth required, matches the public tenant-info endpoint. */
  imageUrl(tenantCode: string, kind: BrandingKind): string {
    return `${environment.apiBaseUrl}/tenants/${encodeURIComponent(tenantCode)}/branding/${kind}?v=${Date.now()}`;
  }
}
