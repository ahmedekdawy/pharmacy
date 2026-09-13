import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { IconButton } from '../../shared/ui/icon-button';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface SettingDto { key: string; value: string; }

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  template: `
  <section class="page">
    <h1>Settings</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    <form [formGroup]="form" (ngSubmit)="save()">
      <input formControlName="key" placeholder="Key (e.g. currency)" />
      <input formControlName="value" placeholder="Value" />
      <app-icon-button icon="save" type="submit" tone="primary" label="Save setting" />
    </form>
    <ul>
      @for (item of items(); track item.key) {
        <li><strong>{{ item.key }}</strong> = {{ item.value }}</li>
      }
    </ul>
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:48rem; margin:0 auto; }
    form { display:grid; grid-template-columns:repeat(auto-fit,minmax(10rem,1fr)); gap:.75rem; margin-bottom:1.25rem; align-items:center; }
    input { padding:.7rem .8rem; border-radius:.35rem; border:1px solid var(--border); font:inherit; background:var(--surface); color:var(--text); }
    ul { list-style:none; padding:0; } li { padding:.75rem 0; border-bottom:1px solid var(--border); }
    .error { color:#c45c4a; }
  `]
})
export class SettingsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly items = signal<SettingDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly form = this.fb.group({
    key: ['', Validators.required],
    value: ['', Validators.required]
  });

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.http.get<ApiResponse<SettingDto[]>>(`${environment.apiBaseUrl}/settings`).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set('Unable to load settings.')
    });
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.http.put(`${environment.apiBaseUrl}/settings`, this.form.getRawValue()).subscribe({
      next: () => { this.form.reset(); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Save failed')
    });
  }
}
