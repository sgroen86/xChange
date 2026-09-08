/**
 * Prototype persistence for the canonical draft.
 *
 * The draft is JSON and small, so sessionStorage is enough and can be read
 * synchronously during render. The source PDF is a Blob and lives in IndexedDB
 * instead — see lib/pdfStore.ts.
 */

import type { ApiProviderExecution, ApiValidationWarning } from "./api";
import type { CanonicalInvoiceDraft } from "./canonical";

const DRAFT_KEY = "xchange.draft.";
const SERVER_KEY = "xchange.server.";

/**
 * What the API returned for an extraction. Kept separate from the draft because
 * it is authoritative and not editable: the XML and the warnings come from
 * deterministic server code, so the review screen shows them rather than
 * recomputing its own version.
 */
export interface ServerExtraction {
  canonicalXml: string;
  warnings: ApiValidationWarning[];
  providerExecution: ApiProviderExecution;
}

export function saveServerResult(invoiceId: string, result: ServerExtraction): void {
  try {
    sessionStorage.setItem(SERVER_KEY + invoiceId, JSON.stringify(result));
  } catch {
    // Storage unavailable: the review screen falls back to local calculation.
  }
}

export function loadServerResult(invoiceId: string): ServerExtraction | null {
  try {
    const raw = sessionStorage.getItem(SERVER_KEY + invoiceId);
    return raw ? (JSON.parse(raw) as ServerExtraction) : null;
  } catch {
    return null;
  }
}

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
