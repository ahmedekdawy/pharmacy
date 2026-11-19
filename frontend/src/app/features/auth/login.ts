import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { TenantService } from '../../core/services/tenant.service';
import { LocalizationService } from '../../core/services/localization.service';
import { IconButton } from '../../shared/ui/icon-button';
import { FieldError } from '../../shared/ui/field-error';
import { apiErrorMessage } from '../../core/http-error';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, IconButton, FieldError],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly i18n = inject(LocalizationService);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  private readonly route = inject(ActivatedRoute);
  private readonly tenantService = inject(TenantService);

  readonly form = this.fb.group({
    tenantCode: [this.initialTenantCode(), [Validators.required, Validators.maxLength(64)]],
    identifier: ['admin@demo.pharmacy', [Validators.required, Validators.maxLength(256)]],
    password: ['Admin@123', [Validators.required, Validators.maxLength(256)]]
  });

  /** Query string `?tenant=` wins, then the last tenant code stored locally, then the demo default. */
  private initialTenantCode(): string {
    const fromQuery = this.route.snapshot.queryParamMap.get('tenant')?.trim();
    if (fromQuery) {
      return fromQuery;
    }
    return this.tenantService.getLastTenantCode()?.trim() || 'DEMO';
  }

  t(key: string): string {
    return this.i18n.translate(key);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    const { tenantCode, identifier, password } = this.form.getRawValue();

    this.auth.login(tenantCode!, identifier!, password!).subscribe({
      next: () => {
        this.loading.set(false);
        void this.router.navigateByUrl('/app/products');
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(apiErrorMessage(err, 'Login failed'));
      }
    });
  }
}
