import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';

interface ApiResponse<T> {
  data: T;
  success: boolean;
  errors: string[];
}

interface LocationDto {
  id: string;
  nameEn: string;
  nameAr: string;
  type: number | string;
  isActive: boolean;
}

@Component({
  selector: 'app-locations',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  templateUrl: './locations.html',
  styleUrl: './locations.scss'
})
export class LocationsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);

  readonly items = signal<LocationDto[]>([]);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    nameEn: ['', Validators.required],
    nameAr: ['', Validators.required],
    type: ['1', Validators.required]
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.http.get<ApiResponse<LocationDto[]>>(`${environment.apiBaseUrl}/locations`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set('Unable to load locations.')
    });
  }

  typeLabel(type: number | string): string {
    const value = Number(type);
    return value === 2 ? 'Warehouse' : value === 1 ? 'Branch' : String(type);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.http.post(`${environment.apiBaseUrl}/locations`, {
      nameEn: this.form.value.nameEn,
      nameAr: this.form.value.nameAr,
      type: Number(this.form.value.type),
      isActive: true
    }).subscribe({
      next: () => {
        this.form.reset({ type: '1' });
        this.reload();
      },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Create failed')
    });
  }
}
