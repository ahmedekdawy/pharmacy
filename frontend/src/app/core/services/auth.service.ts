import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TenantService } from './tenant.service';

export interface PageAccess {
  code: string;
  route: string;
  nameEn: string;
  nameAr: string;
  sortOrder: number;
}

export interface LoginResult {
  accessToken: string;
  expiresAt: string;
  userId: string;
  tenantId: string;
  tenantCode: string;
  email: string;
  fullNameEn: string;
  fullNameAr: string;
  roles: string[];
  permissions: string[];
  pages: PageAccess[];
}

interface ApiResponse<T> {
  data: T;
  success: boolean;
  errors: string[];
}

const TOKEN_KEY = 'pharmacy.access_token';
const USER_KEY = 'pharmacy.auth_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly tenantService = inject(TenantService);

  private readonly session = signal<LoginResult | null>(this.readSession());
  readonly user = computed(() => this.session());
  readonly isAuthenticated = computed(() => !!this.session()?.accessToken);
  readonly pages = computed(() => this.session()?.pages ?? []);
  readonly permissions = computed(() => this.session()?.permissions ?? []);

  login(tenantCode: string, email: string, password: string): Observable<ApiResponse<LoginResult>> {
    return this.http
      .post<ApiResponse<LoginResult>>(`${environment.apiBaseUrl}/auth/login`, {
        tenantCode,
        email,
        password
      })
      .pipe(
        tap((res) => {
          if (!res.success || !res.data) {
            throw new Error(res.errors?.[0] ?? 'Login failed');
          }
          this.persist(res.data);
        })
      );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.tenantService.clear();
    this.session.set(null);
    void this.router.navigateByUrl('/login');
  }

  token(): string | null {
    return this.session()?.accessToken ?? localStorage.getItem(TOKEN_KEY);
  }

  hasPermission(code: string): boolean {
    return this.permissions().some((p) => p.toLowerCase() === code.toLowerCase());
  }

  private persist(data: LoginResult): void {
    localStorage.setItem(TOKEN_KEY, data.accessToken);
    localStorage.setItem(USER_KEY, JSON.stringify(data));
    this.tenantService.setTenantId(data.tenantId);
    this.session.set(data);
  }

  private readSession(): LoginResult | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) {
      return null;
    }
    try {
      return JSON.parse(raw) as LoginResult;
    } catch {
      return null;
    }
  }
}
