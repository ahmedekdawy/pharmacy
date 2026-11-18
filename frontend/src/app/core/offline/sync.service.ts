import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';
import { environment } from '../../../environments/environment';
import { localDb } from './local-db';
import {
  LocalReceipt,
  OutboxOp,
  OutboxType,
  PosCustomer,
  PosLocation,
  PosProduct,
  SyncOpResult,
  SyncSnapshot,
  newId,
  round2
} from './models';

interface ApiResponse<T> {
  data: T;
  success: boolean;
  errors: string[];
}

export type SubmitOutcome =
  | { kind: 'applied'; result: SyncOpResult }
  | { kind: 'queued' }
  | { kind: 'rejected'; error: string };

const SNAPSHOT_KEY = 'snapshot';
const DEVICE_KEY = 'pharmacy.device_id';
const LAST_SYNC_KEY = 'pharmacy.last_sync';
const PUSH_BATCH = 50;
const AUTO_SYNC_MS = 60_000;
const REQUEST_TIMEOUT_MS = 20_000;

/**
 * Local-first data layer for the POS.
 *
 * - Keeps a snapshot of products (with stock), customers (with balances) and locations in IndexedDB
 *   so selling keeps working without internet.
 * - Every write (sale, customer, debt payment) is first attempted online. When the network is not
 *   reachable it is stored in an outbox and uploaded automatically once the connection returns.
 * - Operations carry ids generated on the device, so uploading twice never duplicates anything.
 */
@Injectable({ providedIn: 'root' })
export class SyncService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;
  private started = false;
  private syncPromise: Promise<void> | null = null;

  readonly online = signal(typeof navigator === 'undefined' ? true : navigator.onLine);
  readonly syncing = signal(false);
  readonly outbox = signal<OutboxOp[]>([]);
  readonly pendingCount = computed(() => this.outbox().filter((x) => x.status === 'pending').length);
  readonly failedCount = computed(() => this.outbox().filter((x) => x.status === 'failed').length);
  readonly lastSync = signal<string | null>(localStorage.getItem(LAST_SYNC_KEY));
  readonly lastError = signal<string | null>(null);
  readonly authExpired = signal(false);
  readonly ready = signal(false);

  readonly products = signal<PosProduct[]>([]);
  readonly customers = signal<PosCustomer[]>([]);
  readonly locations = signal<PosLocation[]>([]);

  readonly deviceId = this.resolveDeviceId();

  /** Called once by the app shell after login. */
  async init(): Promise<void> {
    if (this.started) return;
    this.started = true;

    try {
      const snapshot = await localDb.get<SyncSnapshot>('kv', SNAPSHOT_KEY);
      if (snapshot) this.applySnapshot(snapshot);
      await this.reloadOutbox();
    } catch {
      // IndexedDB unavailable (private mode) – the app still works online.
    }
    this.ready.set(true);

    window.addEventListener('online', () => {
      this.online.set(true);
      void this.sync();
    });
    window.addEventListener('offline', () => this.online.set(false));
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') void this.sync();
    });
    setInterval(() => void this.sync(), AUTO_SYNC_MS);

    void this.sync();
  }

  /** Clears all local data (logout / switching pharmacy). */
  async reset(): Promise<void> {
    this.products.set([]);
    this.customers.set([]);
    this.locations.set([]);
    this.outbox.set([]);
    localStorage.removeItem(LAST_SYNC_KEY);
    this.lastSync.set(null);
    try {
      await localDb.clear('kv');
      await localDb.clear('receipts');
      // the outbox is intentionally kept: unsent sales must never be lost
    } catch {
      /* ignore */
    }
  }

  /** Upload pending operations, then download a fresh snapshot. */
  sync(): Promise<void> {
    if (this.syncPromise) return this.syncPromise;
    this.syncPromise = this.runSync().finally(() => (this.syncPromise = null));
    return this.syncPromise;
  }

  /**
   * Try to apply an operation online right away. Business errors (e.g. insufficient stock) are
   * returned as `rejected` so the user can fix them. Network failures queue the operation.
   */
  async submit(type: OutboxType, payload: Record<string, unknown>, label: string): Promise<SubmitOutcome> {
    const op: OutboxOp = {
      opId: newId(),
      type,
      payload: { ...payload, isOffline: false },
      createdAt: new Date().toISOString(),
      status: 'pending',
      attempts: 0,
      label
    };

    // Earlier queued operations must reach the server first (e.g. a customer created offline).
    if (this.online() && this.pendingCount() === 0) {
      try {
        const [result] = await this.pushOps([op]);
        this.online.set(true);
        if (result.status === 'failed') return { kind: 'rejected', error: result.error ?? 'Rejected' };
        this.applyLocalEffects(op);
        await this.persistSnapshot();
        return { kind: 'applied', result };
      } catch (err) {
        if (this.isAuthError(err)) {
          this.authExpired.set(true);
        } else if (!this.isNetworkError(err)) {
          return { kind: 'rejected', error: this.errorMessage(err) };
        }
        this.online.set(false);
      }
    }

    op.payload = { ...op.payload, isOffline: true };
    await localDb.put('outbox', op);
    this.applyLocalEffects(op);
    await this.persistSnapshot();
    await this.reloadOutbox();
    return { kind: 'queued' };
  }

  async retry(opId: string): Promise<void> {
    const op = this.outbox().find((x) => x.opId === opId);
    if (!op) return;
    await localDb.put('outbox', { ...op, status: 'pending', error: null });
    await this.reloadOutbox();
    void this.sync();
  }

  async discard(opId: string): Promise<void> {
    await localDb.delete('outbox', opId);
    await this.reloadOutbox();
    void this.refreshSnapshot().catch(() => undefined);
  }

  // ---------------------------------------------------------------- receipts

  async saveReceipt(receipt: LocalReceipt): Promise<void> {
    try {
      await localDb.put('receipts', receipt);
    } catch {
      /* ignore */
    }
  }

  async receipts(): Promise<LocalReceipt[]> {
    try {
      const all = await localDb.all<LocalReceipt>('receipts');
      return all.sort((a, b) => b.soldAt.localeCompare(a.soldAt));
    } catch {
      return [];
    }
  }

  /** Device-unique invoice number: <device>-<yyMMdd>-<seq>. */
  nextInvoiceNumber(prefix = ''): string {
    const now = new Date();
    const day = `${String(now.getFullYear()).slice(2)}${pad(now.getMonth() + 1)}${pad(now.getDate())}`;
    const key = `pharmacy.seq.${prefix}${day}`;
    const seq = (Number(localStorage.getItem(key)) || 0) + 1;
    localStorage.setItem(key, String(seq));
    return `${prefix}${this.deviceId}-${day}-${String(seq).padStart(4, '0')}`;
  }

  // ---------------------------------------------------------------- internals

  private async runSync(): Promise<void> {
    if (this.syncing()) return;
    this.syncing.set(true);
    this.lastError.set(null);
    try {
      await this.pushOutbox();
      await this.refreshSnapshot();
      this.online.set(true);
      this.authExpired.set(false);
      const now = new Date().toISOString();
      localStorage.setItem(LAST_SYNC_KEY, now);
      this.lastSync.set(now);
    } catch (err) {
      if (this.isAuthError(err)) {
        this.authExpired.set(true);
        this.lastError.set('Session expired – please sign in again to sync.');
      } else if (this.isNetworkError(err)) {
        this.online.set(false);
      } else {
        this.lastError.set(this.errorMessage(err));
      }
    } finally {
      this.syncing.set(false);
    }
  }

  private async pushOutbox(): Promise<void> {
    let ops: OutboxOp[];
    try {
      ops = (await localDb.all<OutboxOp>('outbox'))
        .filter((x) => x.status === 'pending')
        .sort((a, b) => a.createdAt.localeCompare(b.createdAt));
    } catch {
      return;
    }

    for (let i = 0; i < ops.length; i += PUSH_BATCH) {
      const batch = ops.slice(i, i + PUSH_BATCH);
      const results = await this.pushOps(batch);
      for (const op of batch) {
        const result = results.find((r) => r.opId === op.opId);
        if (!result) continue;
        if (result.status === 'failed') {
          await localDb.put('outbox', { ...op, status: 'failed', error: result.error, attempts: op.attempts + 1 });
        } else {
          await localDb.delete('outbox', op.opId);
          await this.markReceiptSynced(op, result);
        }
      }
      await this.reloadOutbox();
    }
  }

  private async pushOps(ops: OutboxOp[]): Promise<SyncOpResult[]> {
    const res = await firstValueFrom(
      this.http
        .post<ApiResponse<{ results: SyncOpResult[] }>>(`${this.base}/sync/push`, {
          deviceId: this.deviceId,
          operations: ops.map((o) => ({ opId: o.opId, type: o.type, payload: o.payload }))
        })
        .pipe(timeout(REQUEST_TIMEOUT_MS))
    );
    return res.data?.results ?? [];
  }

  private async refreshSnapshot(): Promise<void> {
    const res = await firstValueFrom(
      this.http.get<ApiResponse<SyncSnapshot>>(`${this.base}/sync/snapshot`).pipe(timeout(REQUEST_TIMEOUT_MS))
    );
    if (!res.data) return;
    this.applySnapshot(res.data);
    // Operations not yet accepted by the server are re-applied on top of the fresh data.
    for (const op of this.outbox()) this.applyLocalEffects(op);
    await this.persistSnapshot();
  }

  private applySnapshot(snapshot: SyncSnapshot): void {
    this.products.set(snapshot.products ?? []);
    this.customers.set(snapshot.customers ?? []);
    this.locations.set(snapshot.locations ?? []);
  }

  private async persistSnapshot(): Promise<void> {
    try {
      const snapshot: SyncSnapshot = {
        serverTime: new Date().toISOString(),
        products: this.products(),
        customers: this.customers(),
        locations: this.locations()
      };
      await localDb.put('kv', snapshot, SNAPSHOT_KEY);
    } catch {
      /* ignore */
    }
  }

  /** Mirror the effect of an operation locally (stock, balances, new customers). */
  private applyLocalEffects(op: OutboxOp): void {
    const p = op.payload as Record<string, any>;
    if (op.type === 'sale.create') {
      const items = (p['items'] as Array<{ productId: string; quantity: number; baseQuantity?: number }>) ?? [];
      const byProduct = new Map<string, number>();
      for (const item of items) {
        byProduct.set(item.productId, (byProduct.get(item.productId) ?? 0) + Number(item.baseQuantity ?? item.quantity));
      }
      this.products.update((list) =>
        list.map((x) => (byProduct.has(x.id) ? { ...x, stock: round2(x.stock - byProduct.get(x.id)!) } : x))
      );
      const due = Number(p['localDue'] ?? 0);
      if (p['customerId'] && due > 0) this.adjustBalance(p['customerId'], due);
    } else if (op.type === 'customer.create') {
      if (!this.customers().some((c) => c.id === p['id'])) {
        this.customers.update((list) => [
          ...list,
          {
            id: p['id'],
            code: p['code'],
            nameEn: p['nameEn'],
            nameAr: p['nameAr'],
            phone: p['phone'] ?? null,
            address: p['address'] ?? null,
            creditLimit: Number(p['creditLimit'] ?? 0),
            balance: 0,
            isActive: true
          }
        ]);
      }
    } else if (op.type === 'customerPayment.create') {
      this.adjustBalance(p['customerId'], -Number(p['amount'] ?? 0));
    } else if (op.type === 'product.create') {
      if (!this.products().some((x) => x.id === p['id'])) {
        this.products.update((list) => [
          ...list,
          {
            id: p['id'],
            code: p['code'],
            barcode: p['barcode'] ?? null,
            nameEn: p['nameEn'],
            nameAr: p['nameAr'],
            sellingPrice: Number(p['sellingPrice'] ?? 0),
            stripsPerBox: Number(p['stripsPerBox'] ?? 1),
            unitsPerStrip: Number(p['unitsPerStrip'] ?? 1),
            allowStripSale: !!p['allowStripSale'],
            allowUnitSale: !!p['allowUnitSale'],
            stripPrice: p['stripPrice'] ?? null,
            unitPrice: p['unitPrice'] ?? null,
            stock: 0
          }
        ]);
      }
    } else if (op.type === 'openingBalance.create') {
      // quantity is sent in smallest units (unit = 3)
      const qty = Number(p['quantity'] ?? 0);
      this.products.update((list) => list.map((x) => (x.id === p['productId'] ? { ...x, stock: round2(x.stock + qty) } : x)));
    }
  }

  private adjustBalance(customerId: string, delta: number): void {
    this.customers.update((list) =>
      list.map((c) => (c.id === customerId ? { ...c, balance: round2(c.balance + delta) } : c))
    );
  }

  private async markReceiptSynced(op: OutboxOp, result: SyncOpResult): Promise<void> {
    if (op.type !== 'sale.create') return;
    try {
      const receipt = await localDb.get<LocalReceipt>('receipts', String(op.payload['id']));
      if (receipt) {
        await localDb.put('receipts', {
          ...receipt,
          synced: true,
          number: result.number ?? receipt.number,
          warnings: result.warnings ?? []
        });
      }
    } catch {
      /* ignore */
    }
  }

  private async reloadOutbox(): Promise<void> {
    try {
      const ops = await localDb.all<OutboxOp>('outbox');
      this.outbox.set(ops.sort((a, b) => a.createdAt.localeCompare(b.createdAt)));
    } catch {
      /* ignore */
    }
  }

  private isNetworkError(err: unknown): boolean {
    if (err instanceof HttpErrorResponse) return err.status === 0 || err.status >= 500 || err.status === 408;
    return (err as { name?: string })?.name === 'TimeoutError';
  }

  private isAuthError(err: unknown): boolean {
    return err instanceof HttpErrorResponse && (err.status === 401 || err.status === 403);
  }

  private errorMessage(err: unknown): string {
    if (err instanceof HttpErrorResponse) return err.error?.errors?.[0] ?? err.message;
    return (err as Error)?.message ?? 'Unexpected error';
  }

  private resolveDeviceId(): string {
    let id = localStorage.getItem(DEVICE_KEY);
    if (!id) {
      const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
      id = Array.from({ length: 4 }, () => alphabet[Math.floor(Math.random() * alphabet.length)]).join('');
      localStorage.setItem(DEVICE_KEY, id);
    }
    return id;
  }
}

function pad(n: number): string {
  return String(n).padStart(2, '0');
}
