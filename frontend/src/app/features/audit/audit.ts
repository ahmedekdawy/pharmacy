import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { environment } from '../../../environments/environment';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface AuditLogDto {
  id: string; userEmail?: string | null; action: string; entityType: string;
  entityId?: string | null; newValues?: string | null; createdAt: string;
}

@Component({
  selector: 'app-audit',
  standalone: true,
  imports: [DatePipe],
  template: `
  <section class="page">
    <h1>Audit Logs</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    <ul>
      @for (item of items(); track item.id) {
        <li>
          <div>
            <strong>{{ item.action }}</strong> · {{ item.entityType }}
            <span>{{ item.userEmail || 'system' }} · {{ item.createdAt | date:'short' }}</span>
          </div>
          @if (item.newValues) { <pre>{{ item.newValues }}</pre> }
        </li>
      }
    </ul>
  </section>`,
  styles: [`
    :host { display:block; padding:1.5rem; color:var(--text); }
    .page { max-width:64rem; margin:0 auto; }
    ul { list-style:none; padding:0; }
    li { padding:.9rem 0; border-bottom:1px solid var(--border); display:grid; gap:.35rem; }
    span { display:block; opacity:.7; font-size:.85rem; }
    pre { margin:0; white-space:pre-wrap; word-break:break-word; font-size:.8rem; opacity:.85; }
    .error { color:#c45c4a; }
  `]
})
export class AuditPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly items = signal<AuditLogDto[]>([]);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.http.get<ApiResponse<AuditLogDto[]>>(`${environment.apiBaseUrl}/audit-logs`, { params: { take: 100 } }).subscribe({
      next: (res) => this.items.set(res.data ?? []),
      error: () => this.error.set('Unable to load audit logs.')
    });
  }
}
