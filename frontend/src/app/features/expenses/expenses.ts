import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { IconButton } from '../../shared/ui/icon-button';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface Named { id: string; nameEn: string; }
interface ExpenseDto {
  id: string; locationNameEn: string; category: string; descriptionEn: string;
  amount: number; spentAt: string;
}

@Component({
  selector: 'app-expenses',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  template: `
  <section class="page">
    <h1>Expenses</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (auth.hasPermission('Expense.Manage')) {
      <form [formGroup]="form" (ngSubmit)="submit()">
        <select formControlName="locationId">
          <option value="">Location</option>
          @for (l of locations(); track l.id) { <option [value]="l.id">{{ l.nameEn }}</option> }
        </select>
        <input formControlName="category" placeholder="Category" />
        <input formControlName="descriptionEn" placeholder="Description EN" />
        <input formControlName="descriptionAr" placeholder="Description AR" />
        <input formControlName="amount" type="number" step="0.01" placeholder="Amount" />
        <app-icon-button icon="save" type="submit" tone="primary" label="Save expense" />
      </form>
    }
    <ul>
      @for (item of items(); track item.id) {
        <li><strong>{{ item.category }}</strong> — {{ item.descriptionEn }} @ {{ item.locationNameEn }} = {{ item.amount }}</li>
      }
    </ul>
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:64rem; margin:0 auto; }
    form { display:grid; grid-template-columns:repeat(auto-fit,minmax(9rem,1fr)); gap:.75rem; margin-bottom:1.25rem; align-items:center; }
    input,select { padding:.7rem .8rem; border-radius:.35rem; border:1px solid var(--border); font:inherit; background:var(--surface); color:var(--text); }
    ul { list-style:none; padding:0; } li { padding:.75rem 0; border-bottom:1px solid var(--border); }
    .error { color:#c45c4a; }
  `]
})
export class ExpensesPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly items = signal<ExpenseDto[]>([]);
  readonly locations = signal<Named[]>([]);
  readonly error = signal<string | null>(null);
  readonly form = this.fb.group({
    locationId: ['', Validators.required],
    category: ['', Validators.required],
    descriptionEn: ['', Validators.required],
    descriptionAr: ['', Validators.required],
    amount: [0, [Validators.required, Validators.min(0.01)]]
  });

  ngOnInit(): void {
    this.reload();
    this.http.get<ApiResponse<Named[]>>(`${environment.apiBaseUrl}/locations`).subscribe({
      next: (r) => this.locations.set(r.data ?? [])
    });
  }

  reload(): void {
    this.http.get<ApiResponse<ExpenseDto[]>>(`${environment.apiBaseUrl}/expenses`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set('Unable to load expenses.')
    });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    this.http.post(`${environment.apiBaseUrl}/expenses`, {
      ...v,
      amount: Number(v.amount),
      cashShiftId: null
    }).subscribe({
      next: () => { this.form.patchValue({ category: '', descriptionEn: '', descriptionAr: '', amount: 0 }); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Create failed')
    });
  }
}
