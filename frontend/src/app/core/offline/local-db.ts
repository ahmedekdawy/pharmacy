/**
 * Minimal promise-based IndexedDB wrapper used for offline mode.
 *
 * Stores:
 *  - kv      : key/value data (catalog snapshot, metadata)
 *  - outbox  : operations recorded on this device waiting to be uploaded
 *  - receipts: invoices created on this device (for reprint while offline)
 */
const DB_NAME = 'pharmacy-offline';
const DB_VERSION = 1;

export type StoreName = 'kv' | 'outbox' | 'receipts';

let dbPromise: Promise<IDBDatabase> | null = null;

function open(): Promise<IDBDatabase> {
  if (dbPromise) return dbPromise;
  dbPromise = new Promise<IDBDatabase>((resolve, reject) => {
    if (typeof indexedDB === 'undefined') {
      reject(new Error('IndexedDB is not available'));
      return;
    }
    const req = indexedDB.open(DB_NAME, DB_VERSION);
    req.onupgradeneeded = () => {
      const db = req.result;
      if (!db.objectStoreNames.contains('kv')) db.createObjectStore('kv');
      if (!db.objectStoreNames.contains('outbox')) {
        const outbox = db.createObjectStore('outbox', { keyPath: 'opId' });
        outbox.createIndex('createdAt', 'createdAt');
      }
      if (!db.objectStoreNames.contains('receipts')) {
        const receipts = db.createObjectStore('receipts', { keyPath: 'id' });
        receipts.createIndex('soldAt', 'soldAt');
      }
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => {
      dbPromise = null;
      reject(req.error);
    };
  });
  return dbPromise;
}

function run<T>(store: StoreName, mode: IDBTransactionMode, fn: (s: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  return open().then(
    (db) =>
      new Promise<T>((resolve, reject) => {
        const tx = db.transaction(store, mode);
        const request = fn(tx.objectStore(store));
        tx.oncomplete = () => resolve(request.result);
        tx.onerror = () => reject(tx.error);
        tx.onabort = () => reject(tx.error);
      })
  );
}

export const localDb = {
  get<T>(store: StoreName, key: IDBValidKey): Promise<T | undefined> {
    return run<T | undefined>(store, 'readonly', (s) => s.get(key));
  },
  put<T>(store: StoreName, value: T, key?: IDBValidKey): Promise<IDBValidKey> {
    return run(store, 'readwrite', (s) => (key === undefined ? s.put(value) : s.put(value, key)));
  },
  delete(store: StoreName, key: IDBValidKey): Promise<undefined> {
    return run(store, 'readwrite', (s) => s.delete(key));
  },
  all<T>(store: StoreName): Promise<T[]> {
    return run<T[]>(store, 'readonly', (s) => s.getAll());
  },
  count(store: StoreName): Promise<number> {
    return run<number>(store, 'readonly', (s) => s.count());
  },
  clear(store: StoreName): Promise<undefined> {
    return run(store, 'readwrite', (s) => s.clear());
  }
};
