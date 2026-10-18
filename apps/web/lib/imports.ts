/**
 * Client for batch import: PDFs are accepted at once and read in the
 * background; the "te beoordelen" list shows where each one is. Opening a
 * ready import turns it into an ordinary review draft in this browser.
 */

import { API_BASE_URL, ApiError, isApiConfigured, type ApiValidationWarning } from "./api";
import { authHeaders } from "./auth";
import { normalizeInvoice, type ReviewDraft } from "./canonical";
import { putPdf } from "./pdfStore";
import { saveDraft, saveServerResult } from "./store";

export type ImportStatus = "awaitingApproval" | "queued" | "processing" | "ready" | "failed";

export interface ImportItem {
  importId: string;
  source: "upload" | "email";
  status: ImportStatus;
  fileName: string;
  byteSize: number;
  sender: string | null;
  subject: string | null;
  error: string | null;
  extractionId: string | null;
  invoiceNumber: string | null;
  buyerName: string | null;
  providerModel: string | null;
  createdAt: string;
  /** Only when one import is fetched. */
  canonicalInvoice?: unknown;
  warnings?: ApiValidationWarning[];
}

async function call(path: string, init?: RequestInit): Promise<Response> {
  if (!isApiConfigured) throw new ApiError("Er is geen API geconfigureerd.", 0);

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers: { ...(init?.headers ?? {}), ...authHeaders() } });
  } catch {
    throw new ApiError("De API is niet bereikbaar.", 0);
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as { detail?: string } | null;
    throw new ApiError(problem?.detail ?? `De API gaf status ${response.status}.`, response.status);
  }
  return response;
}

export async function submitImport(file: File): Promise<ImportItem> {
  const body = new FormData();
  body.append("file", file, file.name);
  // No Content-Type: the browser adds the multipart boundary itself.
  return (await (await call("/api/v1/imports", { method: "POST", body })).json()) as ImportItem;
}

export async function listImports(): Promise<ImportItem[]> {
  if (!isApiConfigured) return [];
  return (await (await call("/api/v1/imports")).json()) as ImportItem[];
}

/** A mailed PDF is only read after a user approves it. */
export async function approveImport(importId: string): Promise<void> {
  await call(`/api/v1/imports/${encodeURIComponent(importId)}/approve`, { method: "POST" });
}

/** Opens the PDF in a new tab, to look at a mailed one before approving it. */
export async function viewImportDocument(importId: string): Promise<void> {
  const target = window.open("", "_blank");
  const blob = await (await call(`/api/v1/imports/${encodeURIComponent(importId)}/document`)).blob();
  const url = URL.createObjectURL(new Blob([blob], { type: "application/pdf" }));
  if (target) target.location.href = url;
  else window.location.href = url;
  window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

export async function retryImport(importId: string): Promise<void> {
  await call(`/api/v1/imports/${encodeURIComponent(importId)}/retry`, { method: "POST" });
}

export async function dismissImport(importId: string): Promise<void> {
  await call(`/api/v1/imports/${encodeURIComponent(importId)}`, { method: "DELETE" });
}

/**
 * Turns a ready import into a review draft in this browser - the draft, its
 * notices and the PDF - so the review screen opens it like a fresh upload.
 * The import id is the invoice id: booking stores the invoice under it.
 */
export async function openImport(importId: string): Promise<void> {
  const item = (await (await call(`/api/v1/imports/${encodeURIComponent(importId)}`)).json()) as ImportItem;
  if (item.status !== "ready" || !item.canonicalInvoice) {
    throw new ApiError("Deze factuur is nog niet gelezen.", 409);
  }

  const pdf = await (await call(`/api/v1/imports/${encodeURIComponent(importId)}/document`)).blob();
  await putPdf(importId, new File([pdf], item.fileName, { type: "application/pdf" }));

  const [provider, ...model] = (item.providerModel ?? "onbekend").split("/");
  const draft: ReviewDraft = {
    id: importId,
    extractionId: item.extractionId ?? undefined,
    fromImport: true,
    invoice: normalizeInvoice(item.canonicalInvoice),
    source: {
      fileName: item.fileName,
      byteSize: item.byteSize,
      engine: item.providerModel ?? "onbekend",
      extractedAt: item.createdAt,
    },
  };
  saveDraft(draft);
  saveServerResult(importId, {
    canonicalXml: "",
    warnings: item.warnings ?? [],
    providerExecution: { provider, model: model.join("/"), durationMs: 0 },
  });
}
