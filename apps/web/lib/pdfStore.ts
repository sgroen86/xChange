/**
 * Durable PDF storage for the review screen.
 *
 * An in-memory object URL does not survive a page load, and on the static
 * export a navigation to a non-prerendered invoice id can be a full document
 * load rather than a client-side route change — so the source PDF has to be
 * persisted, not held in a module variable.
 *
 * IndexedDB rather than sessionStorage: it stores Blobs directly, where
 * sessionStorage would need base64 (a third larger) inside a ~5MB budget that a
 * 15MB upload would blow straight through.
 */

const DB_NAME = "xchange";
const STORE = "pdfs";
const DB_VERSION = 1;

/** Keep the tab's recent uploads, discard the rest. */
const MAX_ENTRIES = 5;

export interface StoredPdf {
  id: string;
  name: string;
  type: string;
  size: number;
  storedAt: number;
  blob: Blob;
}

function openDb(): Promise<IDBDatabase | null> {
  return new Promise((resolve) => {
    if (typeof indexedDB === "undefined") {
      resolve(null);
      return;
    }

    let request: IDBOpenDBRequest;
    try {
      request = indexedDB.open(DB_NAME, DB_VERSION);
    } catch {
      resolve(null); // Blocked in some private-browsing modes.
      return;
    }

    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(STORE)) {
        const store = db.createObjectStore(STORE, { keyPath: "id" });
        store.createIndex("storedAt", "storedAt");
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => resolve(null);
    request.onblocked = () => resolve(null);
  });
}

function done(tx: IDBTransaction): Promise<void> {
  return new Promise((resolve) => {
    tx.oncomplete = () => resolve();
    tx.onerror = () => resolve();
    tx.onabort = () => resolve();
  });
}

/** Store the source PDF for an invoice. Resolves once the write is committed. */
export async function putPdf(invoiceId: string, file: File): Promise<void> {
  const db = await openDb();
  if (!db) return;

  const record: StoredPdf = {
    id: invoiceId,
    name: file.name,
    type: file.type || "application/pdf",
    size: file.size,
    storedAt: Date.now(),
    blob: file,
  };

  const tx = db.transaction(STORE, "readwrite");
  tx.objectStore(STORE).put(record);
  await done(tx);

  await prune(db);
  db.close();
}

export async function getPdf(invoiceId: string): Promise<StoredPdf | null> {
  const db = await openDb();
  if (!db) return null;

  const record = await new Promise<StoredPdf | null>((resolve) => {
    const request = db.transaction(STORE, "readonly").objectStore(STORE).get(invoiceId);
    request.onsuccess = () => resolve((request.result as StoredPdf) ?? null);
    request.onerror = () => resolve(null);
  });

  db.close();
  return record;
}

/** Drop the oldest entries so uploads do not accumulate indefinitely. */
async function prune(db: IDBDatabase): Promise<void> {
  const ids = await new Promise<string[]>((resolve) => {
    const request = db.transaction(STORE, "readonly").objectStore(STORE).index("storedAt").getAllKeys();
    request.onsuccess = () => resolve((request.result as IDBValidKey[]).map(String));
    request.onerror = () => resolve([]);
  });

  if (ids.length <= MAX_ENTRIES) return;

  const tx = db.transaction(STORE, "readwrite");
  const store = tx.objectStore(STORE);
  // getAllKeys on the storedAt index returns oldest first.
  for (const id of ids.slice(0, ids.length - MAX_ENTRIES)) {
    store.delete(id);
  }
  await done(tx);
}
