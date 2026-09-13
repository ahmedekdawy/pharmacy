import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { IconButton } from '../../shared/ui/icon-button';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, IconButton],
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

  readonly form = this.fb.group({
    tenantCode: ['DEMO', Validators.required],
    email: ['admin@demo.pharmacy', [Validators.required, Validators.email]],
    password: ['Admin@123', Validators.required]
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
    const { tenantCode, email, password } = this.form.getRawValue();

    this.auth.login(tenantCode!, email!, password!).subscribe({
      next: () => {
        this.loading.set(false);
        void this.router.navigateByUrl('/app/products');
      },
      error: (err) => {
        this.loading.set(false);
        const message =
          err?.error?.errors?.[0] ??
          err?.message ??
          'Login failed';
        this.error.set(message);
      }
    });
  }
}
