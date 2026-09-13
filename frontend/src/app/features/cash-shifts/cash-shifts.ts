import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface Named { id: string; nameEn: string; }
interface CashShiftDto {
  id: string; locationNameEn: string; openedAt: string; closedAt?: string | null;
  openingCash: number; closingCash?: number | null; expectedCash?: number | null; status: string;
}

@Component({
  selector: 'app-cash-shifts',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  template: `
  <section class="page">
    <h1>Cash Shifts</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (auth.hasPermission('CashShift.Open')) {
      <form [formGroup]="openForm" (ngSubmit)="open()">
        <select formControlName="locationId">
          <option value="">Location</option>
          @for (l of locations(); track l.id) { <option [value]="l.id">{{ l.nameEn }}</option> }
        </select>
        <input formControlName="openingCash" type="number" step="0.01" placeholder="Opening cash" />
        <app-icon-button icon="add" type="submit" tone="primary" label="Open shift" />
      </form>
    }
    <ul>
      @for (item of items(); track item.id) {
        <li>
          <strong>{{ item.locationNameEn }}</strong> — {{ item.status }}
          open {{ item.openingCash }}
          @if (item.expectedCash != null) { / expected {{ item.expectedCash }} }
          @if (item.closingCash != null) { / closed {{ item.closingCash }} }
          @if (item.status === 'Open' && auth.hasPermission('CashShift.Close')) {
            <span class="close-row">
              <input [id]="'close-' + item.id" type="number" step="0.01" placeholder="Closing cash" #closing />
              <app-icon-button icon="check" tone="success" label="Close shift" (pressed)="close(item.id, closing.value)" />
            </span>
          }
        </li>
      }
    </ul>
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:64rem; margin:0 auto; }
    form,.close-row { display:flex; gap:.75rem; flex-wrap:wrap; margin:.5rem 0 1rem; align-items:center; }
    input,select { padding:.7rem .8rem; border-radius:.35rem; border:1px solid var(--border); font:inherit; background:var(--surface); color:var(--text); }
    ul { list-style:none; padding:0; } li { padding:.9rem 0; border-bottom:1px solid var(--border); }
    .error { color:#c45c4a; }
  `]
})
export class CashShiftsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly items = signal<CashShiftDto[]>([]);
  readonly locations = signal<Named[]>([]);
  readonly error = signal<string | null>(null);
  readonly openForm = this.fb.group({
    locationId: ['', Validators.required],
    openingCash: [0, [Validators.required, Validators.min(0)]]
  });

  ngOnInit(): void {
    this.reload();
    this.http.get<ApiResponse<Named[]>>(`${environment.apiBaseUrl}/locations`).subscribe({
      next: (r) => this.locations.set(r.data ?? [])
    });
  }

  reload(): void {
    this.http.get<ApiResponse<CashShiftDto[]>>(`${environment.apiBaseUrl}/cash-shifts`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set('Unable to load cash shifts.')
    });
  }

  open(): void {
    if (this.openForm.invalid) { this.openForm.markAllAsTouched(); return; }
    const v = this.openForm.getRawValue();
    this.http.post(`${environment.apiBaseUrl}/cash-shifts/open`, {
      locationId: v.locationId,
      openingCash: Number(v.openingCash),
      notes: null
    }).subscribe({
      next: () => { this.openForm.patchValue({ openingCash: 0 }); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Open failed')
    });
  }

  close(id: string, closingCash: string): void {
    const amount = Number(closingCash);
    if (Number.isNaN(amount) || amount < 0) {
      this.error.set('Enter a valid closing cash amount.');
      return;
    }
    this.http.post(`${environment.apiBaseUrl}/cash-shifts/${id}/close`, {
      closingCash: amount,
      notes: null
    }).subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Close failed')
    });
  }
}
