/**
 * Prototype persistence.
 *
 * The draft is JSON and goes to sessionStorage so a reload keeps the data.
 * The PDF itself stays in memory for the lifetime of the tab — object URLs are
 * not serialisable, so after a hard reload the viewer shows an empty state
 * rather than a broken frame.
 */

import type { CanonicalInvoiceDraft } from "./canonical";

const DRAFT_KEY = "xchange.draft.";

const pdfObjectUrls = new Map<string, string>();

export function putPdf(invoiceId: string, file: File): void {
  const existing = pdfObjectUrls.get(invoiceId);
  if (existing) URL.revokeObjectURL(existing);
  pdfObjectUrls.set(invoiceId, URL.createObjectURL(file));
}

export function getPdfUrl(invoiceId: string): string | null {
  return pdfObjectUrls.get(invoiceId) ?? null;
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
