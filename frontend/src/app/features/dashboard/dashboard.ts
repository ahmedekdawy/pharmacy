import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface DashboardDto {
  productCount: number;
  lowStockCount: number;
  nearExpiryCount: number;
  todaySalesTotal: number;
  todaySalesCount: number;
  openCashShifts: number;
  monthExpensesTotal: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DecimalPipe, RouterLink],
  template: `
  <section class="page">
    <h1>Dashboard</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (data(); as d) {
      <div class="grid">
        <a class="tile" routerLink="/app/products"><span>Products</span><strong>{{ d.productCount }}</strong></a>
        <a class="tile" routerLink="/app/inventory"><span>Low stock</span><strong>{{ d.lowStockCount }}</strong></a>
        <a class="tile" routerLink="/app/inventory"><span>Near expiry</span><strong>{{ d.nearExpiryCount }}</strong></a>
        <a class="tile" routerLink="/app/sales"><span>Today sales</span><strong>{{ d.todaySalesCount }} / {{ d.todaySalesTotal | number:'1.2-2' }}</strong></a>
        <a class="tile" routerLink="/app/cash-shifts"><span>Open shifts</span><strong>{{ d.openCashShifts }}</strong></a>
        <a class="tile" routerLink="/app/expenses"><span>Month expenses</span><strong>{{ d.monthExpensesTotal | number:'1.2-2' }}</strong></a>
      </div>
    }
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:64rem; margin:0 auto; }
    .grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(12rem,1fr)); gap:1rem; }
    .tile {
      display:grid; gap:.45rem; padding:1.1rem 1.2rem; border-radius:.7rem;
      border:1px solid var(--border); background:var(--surface); color:inherit; text-decoration:none;
    }
    .tile span { opacity:.72; font-size:.9rem; }
    .tile strong { font-size:1.35rem; }
    .error { color:#c45c4a; }
  `]
})
export class DashboardPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly data = signal<DashboardDto | null>(null);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.http.get<ApiResponse<DashboardDto>>(`${environment.apiBaseUrl}/dashboard`).subscribe({
      next: (res) => this.data.set(res.data),
      error: () => this.error.set('Unable to load dashboard.')
    });
  }
}
