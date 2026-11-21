import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';
import { ApiService, EgyptianDrug } from '../services/api.service';
import { localDb } from '../offline/local-db';

const CACHE_KEY = 'egyptianDrugs';
const REQUEST_TIMEOUT_MS = 20_000;

/**
 * Local-first cache of the external Egyptian drug reference list (used in POS to suggest
 * adding a drug that isn't in the pharmacy's own catalog yet). Same approach as SyncService:
 * load the cached copy immediately so it works offline, then replace it with a fresh download
 * whenever online so the cache stays current.
 */
@Injectable({ providedIn: 'root' })
export class EgyptianDrugCatalogService {
  private readonly api = inject(ApiService);
  private started = false;
  private refreshPromise: Promise<void> | null = null;

  readonly drugs = signal<EgyptianDrug[]>([]);
  readonly ready = signal(false);

  async init(): Promise<void> {
    if (this.started) return;
    this.started = true;

    try {
      const cached = await localDb.get<EgyptianDrug[]>('kv', CACHE_KEY);
      if (cached?.length) this.drugs.set(cached);
    } catch {
      // IndexedDB unavailable (private mode) – falls back to online-only.
    }
    this.ready.set(true);

    void this.refresh();
  }

  /** Re-downloads the full list and replaces the cache. Resolves quietly if offline/unreachable. */
  refresh(): Promise<void> {
    if (this.refreshPromise) return this.refreshPromise;
    this.refreshPromise = this.runRefresh().finally(() => (this.refreshPromise = null));
    return this.refreshPromise;
  }

  /** Same multi-word, any-order matching the backend uses, so this works offline too. */
  search(term: string, limit = 20): EgyptianDrug[] {
    const words = term.trim().toLowerCase().split(/\s+/).filter(Boolean);
    if (!words.length) return [];
    return this.drugs()
      .filter((d) => {
        const haystack = `${d.commercialNameEn} ${d.commercialNameAr} ${d.scientificName} ${d.manufacturer}`.toLowerCase();
        return words.every((w) => haystack.includes(w));
      })
      .slice(0, limit);
  }

  private async runRefresh(): Promise<void> {
    try {
      const res = await firstValueFrom(this.api.getAllEgyptianDrugs().pipe(timeout(REQUEST_TIMEOUT_MS)));
      const drugs = res.data ?? [];
      if (!drugs.length) return;

      this.drugs.set(drugs);
      try {
        await localDb.put('kv', drugs, CACHE_KEY);
      } catch {
        /* ignore */
      }
    } catch {
      // Offline or server unreachable: keep serving whatever is already cached.
    }
  }
}
