/**
 * Client for stored invoices.
 *
 * The organisation is never sent: the API takes it from the session, so the
 * browser cannot ask for another tenant's data by changing a parameter.
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders } from "./auth";
import type { CanonicalInvoiceDraft } from "./canonical";

export interface StoredInvoice {
  invoiceId: string;
  status: "Draft" | "Booked";
  canonicalInvoice: Record<string, unknown>;
  sourceFileName: string | null;
  providerModel: string | null;
  bookedAt: string | null;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  if (!isApiConfigured) {
    throw new ApiError("Er is geen API geconfigureerd.", 0);
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      headers: { ...(init?.headers ?? {}), ...authHeaders() },
    });
  } catch {
    throw new ApiError("De API is niet bereikbaar.", 0);
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as { detail?: string } | null;
    throw new ApiError(problem?.detail ?? `De API gaf status ${response.status}.`, response.status);
  }

  return (await response.json()) as T;
}

/** Base64 without the data: prefix, which the API does not expect. */
async function toBase64(blob: Blob): Promise<string> {
  const buffer = await blob.arrayBuffer();
  const bytes = new Uint8Array(buffer);
  let binary = "";
  for (let index = 0; index < bytes.length; index++) {
    binary += String.fromCharCode(bytes[index]);
  }
  return btoa(binary);
}

export async function bookInvoice(input: {
  invoiceId?: string;
  canonicalInvoice: CanonicalInvoiceDraft;
  pdf: Blob;
  sourceFileName: string;
  providerModel?: string;
}): Promise<StoredInvoice> {
  return request<StoredInvoice>("/api/v1/invoices", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      invoiceId: input.invoiceId,
      canonicalInvoice: input.canonicalInvoice,
      documentBase64: await toBase64(input.pdf),
      sourceFileName: input.sourceFileName,
      providerModel: input.providerModel,
    }),
  });
}

export function listInvoices(): Promise<StoredInvoice[]> {
  return request<StoredInvoice[]>("/api/v1/invoices");
}

export function getInvoice(invoiceId: string): Promise<StoredInvoice> {
  return request<StoredInvoice>(`/api/v1/invoices/${invoiceId}`);
}

/**
 * Fetches the stored PDF as a blob URL.
 *
 * The endpoint requires the session token, so the URL cannot simply be handed
 * to an iframe: the browser would request it without the header and get a 401.
 * The caller must revoke the returned URL when done with it.
 */
export async function documentBlobUrl(invoiceId: string): Promise<string | null> {
  if (!isApiConfigured) return null;

  try {
    const response = await fetch(`${API_BASE_URL}/api/v1/invoices/${invoiceId}/document`, {
      headers: authHeaders(),
    });
    if (!response.ok) return null;
    return URL.createObjectURL(await response.blob());
  } catch {
    return null;
  }
}
