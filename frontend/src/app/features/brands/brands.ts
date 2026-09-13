import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface NamedDto { id: string; nameEn: string; nameAr: string; isActive: boolean; }

@Component({
  selector: 'app-brands',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  templateUrl: './brands.html',
  styleUrl: './brands.scss'
})
export class BrandsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly items = signal<NamedDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly form = this.fb.group({
    nameEn: ['', Validators.required],
    nameAr: ['', Validators.required]
  });

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.http.get<ApiResponse<NamedDto[]>>(`${environment.apiBaseUrl}/brands`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set('Unable to load brands.')
    });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.http.post(`${environment.apiBaseUrl}/brands`, { ...this.form.getRawValue(), isActive: true }).subscribe({
      next: () => { this.form.reset(); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Create failed')
    });
  }
}
