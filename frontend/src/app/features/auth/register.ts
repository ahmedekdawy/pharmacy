import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { IconButton } from '../../shared/ui/icon-button';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, IconButton],
  templateUrl: './register.html',
  styleUrl: './register.scss'
})
export class Register {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly i18n = inject(LocalizationService);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  readonly form = this.fb.group({
    tenantNameEn: ['', Validators.required],
    tenantNameAr: ['', Validators.required],
    tenantCode: ['', Validators.required],
    adminFullNameEn: ['', Validators.required],
    adminFullNameAr: ['', Validators.required],
    adminEmail: ['', [Validators.required, Validators.email]],
    adminPassword: ['', [Validators.required, Validators.minLength(8)]]
  });

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
    const value = this.form.getRawValue();

    this.auth
      .registerTenant({
        tenantNameEn: value.tenantNameEn!,
        tenantNameAr: value.tenantNameAr!,
        tenantCode: value.tenantCode!,
        adminFullNameEn: value.adminFullNameEn!,
        adminFullNameAr: value.adminFullNameAr!,
        adminEmail: value.adminEmail!,
        adminPassword: value.adminPassword!
      })
      .subscribe({
        next: () => {
          this.loading.set(false);
          void this.router.navigateByUrl('/app/products');
        },
        error: (err) => {
          this.loading.set(false);
          const message =
            err?.error?.errors?.[0] ??
            err?.message ??
            'Registration failed';
          this.error.set(message);
        }
      });
  }
}
