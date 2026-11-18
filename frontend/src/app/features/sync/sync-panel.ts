import { DatePipe } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { LocalizationService } from '../../core/services/localization.service';
import { SyncService } from '../../core/offline/sync.service';

/** Shows what is waiting to be uploaded from this device and lets the user retry or discard failures. */
@Component({
  selector: 'app-sync-panel',
  standalone: true,
  imports: [DatePipe],
  template: `
  <section class="page">
    <header class="head">
      <h1>{{ pick('Sync', 'المزامنة') }}</h1>
      <button type="button" class="btn primary" (click)="sync.sync()" [disabled]="sync.syncing()">
        {{ sync.syncing() ? pick('Syncing…', 'جاري المزامنة…') : pick('Sync now', 'مزامنة الآن') }}
      </button>
    </header>

    <dl class="status">
      <div><dt>{{ pick('Connection', 'الاتصال') }}</dt><dd [class.off]="!sync.online()">{{ sync.online() ? pick('Online', 'متصل') : pick('Offline', 'غير متصل') }}</dd></div>
      <div><dt>{{ pick('Device code', 'كود الجهاز') }}</dt><dd>{{ sync.deviceId }}</dd></div>
      <div><dt>{{ pick('Last sync', 'آخر مزامنة') }}</dt><dd>{{ sync.lastSync() ? (sync.lastSync() | date: 'yyyy-MM-dd HH:mm') : '—' }}</dd></div>
      <div><dt>{{ pick('Products on device', 'أصناف على الجهاز') }}</dt><dd>{{ sync.products().length }}</dd></div>
      <div><dt>{{ pick('Waiting to upload', 'في انتظار الرفع') }}</dt><dd>{{ sync.pendingCount() }}</dd></div>
      <div><dt>{{ pick('Failed', 'فشلت') }}</dt><dd [class.off]="sync.failedCount() > 0">{{ sync.failedCount() }}</dd></div>
    </dl>

    @if (sync.authExpired()) {
      <p class="error">{{ pick('Your session expired. Sign out and sign in again to upload pending operations – they are kept safely on this device.', 'انتهت الجلسة. سجّل الخروج ثم الدخول مرة أخرى لرفع العمليات المعلقة – وهي محفوظة على الجهاز.') }}</p>
    }
    @if (sync.lastError()) { <p class="error">{{ sync.lastError() }}</p> }

    <ul class="ops">
      @for (op of sync.outbox(); track op.opId) {
        <li [class.failed]="op.status === 'failed'">
          <div>
            <strong>{{ op.label }}</strong>
            <small>{{ op.createdAt | date: 'yyyy-MM-dd HH:mm' }} · {{ op.status === 'failed' ? pick('Failed', 'فشلت') : pick('Pending', 'معلقة') }}</small>
            @if (op.error) { <small class="err">{{ op.error }}</small> }
          </div>
          @if (op.status === 'failed') {
            <div class="actions">
              <button type="button" class="btn" (click)="sync.retry(op.opId)">{{ pick('Retry', 'إعادة المحاولة') }}</button>
              <button type="button" class="btn danger" (click)="discard(op.opId)">{{ pick('Discard', 'تجاهل') }}</button>
            </div>
          }
        </li>
      } @empty {
        <li class="hint">{{ pick('Everything on this device is uploaded.', 'كل البيانات على هذا الجهاز تم رفعها.') }}</li>
      }
    </ul>
  </section>`,
  styles: [`
    :host { display:block; padding:1rem; color:var(--text); }
    .page { max-width:48rem; margin:0 auto; display:grid; gap:.8rem; }
    .head { display:flex; justify-content:space-between; align-items:center; gap:.5rem; } h1 { margin:0; }
    .status { display:grid; grid-template-columns:repeat(auto-fill,minmax(9rem,1fr)); gap:.5rem; margin:0; }
    .status div { background:var(--surface); border:1px solid var(--border); border-radius:.55rem; padding:.6rem; }
    dt { font-size:.8rem; color:var(--muted); } dd { margin:0; font-weight:600; } dd.off { color:#b5462f; }
    .ops { list-style:none; padding:0; margin:0; display:grid; gap:.45rem; }
    .ops li { display:flex; justify-content:space-between; gap:.5rem; align-items:center; background:var(--surface); border:1px solid var(--border); border-radius:.55rem; padding:.6rem; }
    .ops li.failed { border-color:#c45c4a; }
    .ops li div:first-child { display:flex; flex-direction:column; gap:.15rem; } small { color:var(--muted); } .err { color:#c45c4a; }
    .actions { display:flex; gap:.35rem; }
    .btn { border:1px solid var(--border); background:var(--surface-muted); color:var(--text); border-radius:.45rem; padding:.5rem .8rem; font:inherit; cursor:pointer; }
    .btn.primary { background:var(--accent); color:#fff; border-color:transparent; } .btn.danger { color:#c45c4a; }
    .error { color:#c45c4a; } .hint { color:var(--muted); }
  `]
})
export class SyncPanelPage implements OnInit {
  readonly i18n = inject(LocalizationService);
  readonly sync = inject(SyncService);

  ngOnInit(): void {
    void this.sync.init();
  }

  pick(en: string, ar: string): string {
    return this.i18n.pick(en, ar);
  }

  discard(opId: string): void {
    if (confirm(this.pick('Discard this operation? It will never reach the server.', 'تجاهل هذه العملية؟ لن يتم رفعها للسيرفر نهائياً.'))) {
      void this.sync.discard(opId);
    }
  }
}
