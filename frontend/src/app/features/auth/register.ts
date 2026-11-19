import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { IconButton } from '../../shared/ui/icon-button';
import { FieldError } from '../../shared/ui/field-error';
import { apiErrorMessage } from '../../core/http-error';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, IconButton, FieldError],
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
    tenantNameEn: ['', [Validators.required, Validators.maxLength(256)]],
    tenantNameAr: ['', [Validators.required, Validators.maxLength(256)]],
    tenantCode: ['', [Validators.required, Validators.maxLength(64)]],
    adminFullNameEn: ['', [Validators.required, Validators.maxLength(256)]],
    adminFullNameAr: ['', [Validators.required, Validators.maxLength(256)]],
    adminEmail: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    adminPhoneNumber: ['', [Validators.maxLength(32)]],
    adminPassword: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(256)]]
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
        adminPhoneNumber: value.adminPhoneNumber || undefined,
        adminPassword: value.adminPassword!
      })
      .subscribe({
        next: () => {
          this.loading.set(false);
          void this.router.navigateByUrl('/app/products');
        },
        error: (err) => {
          this.loading.set(false);
          this.error.set(apiErrorMessage(err, 'Registration failed'));
        }
      });
  }
}
