import { Injectable, signal } from '@angular/core';

const TENANT_CODE_KEY = 'pharmacy.tenant_code';

@Injectable({ providedIn: 'root' })
export class TenantService {
  readonly tenantId = signal<string | null>(this.readStoredTenant());

  setTenantId(tenantId: string): void {
    this.tenantId.set(tenantId);
    localStorage.setItem('pharmacy.tenant_id', tenantId);
  }

  clear(): void {
    this.tenantId.set(null);
    localStorage.removeItem('pharmacy.tenant_id');
  }

  /** Last tenant code used to sign in. Kept across logout so the login form can prefill it. */
  getLastTenantCode(): string | null {
    try {
      return localStorage.getItem(TENANT_CODE_KEY);
    } catch {
      return null;
    }
  }

  setLastTenantCode(tenantCode: string): void {
    try {
      localStorage.setItem(TENANT_CODE_KEY, tenantCode);
    } catch {
      /* storage unavailable */
    }
  }

  private readStoredTenant(): string | null {
    return localStorage.getItem('pharmacy.tenant_id');
  }
}
