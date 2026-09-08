/**
 * Prototype persistence for the canonical draft.
 *
 * The draft is JSON and small, so sessionStorage is enough and can be read
 * synchronously during render. The source PDF is a Blob and lives in IndexedDB
 * instead — see lib/pdfStore.ts.
 */

import type { CanonicalInvoiceDraft } from "./canonical";

const DRAFT_KEY = "xchange.draft.";

export function saveDraft(draft: CanonicalInvoiceDraft): void {
  try {
    sessionStorage.setItem(DRAFT_KEY + draft.id, JSON.stringify(draft));
  } catch {
    // Private browsing or blocked storage: the in-page state still works.
  }
}

export function loadDraft(invoiceId: string): CanonicalInvoiceDraft | null {
  try {
    const raw = sessionStorage.getItem(DRAFT_KEY + invoiceId);
    return raw ? (JSON.parse(raw) as CanonicalInvoiceDraft) : null;
  } catch {
    return null;
  }
}

export function newInvoiceId(): string {
  const stamp = Date.now().toString(36);
  const noise = Math.random().toString(36).slice(2, 6);
  return `inv-${stamp}${noise}`;
}
