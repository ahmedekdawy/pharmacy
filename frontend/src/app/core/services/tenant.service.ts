import { Injectable, signal } from '@angular/core';

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

  private readStoredTenant(): string | null {
    return localStorage.getItem('pharmacy.tenant_id');
  }
}
