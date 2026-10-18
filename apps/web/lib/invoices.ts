/**
 * Client for stored invoices, checks and exports.
 *
 * The organisation is never sent: the API takes it from the session, so the
 * browser cannot ask for another tenant's data by changing a parameter.
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders, clearSession } from "./auth";
import type { CanonicalInvoice } from "./canonical";

export interface StoredInvoice {
  invoiceId: string;
  status: "Draft" | "Booked";
  /** The server's CanonicalInvoiceDraft; pass it through normalizeInvoice before use. */
  canonicalInvoice: unknown;
  sourceFileName: string | null;
  providerModel: string | null;
  bookedAt: string | null;
}

/** A rule the invoice breaks, from the server's ruleset. */
export interface RuleViolation {
  ruleId: string;
  businessTerm: string | null;
  /** Dotted field path; equal to the id of the review screen's input. */
  path: string;
  message: string;
}

async function send(path: string, init?: RequestInit): Promise<Response> {
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

  if (response.status === 401) {
    clearSession();
    throw new ApiError("Uw sessie is verlopen. Log opnieuw in.", 401);
  }

  return response;
}

async function failure(response: Response): Promise<ApiError> {
  const problem = (await response.json().catch(() => null)) as { detail?: string } | null;
  return new ApiError(problem?.detail ?? `De API gaf status ${response.status}.`, response.status);
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await send(path, init);
  if (!response.ok) throw await failure(response);
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
  canonicalInvoice: CanonicalInvoice;
  pdf: Blob;
  sourceFileName: string;
  providerModel?: string;
  /** The upload this invoice came from, so xChange learns from what was corrected. */
  extractionId?: string;
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
      extractionId: input.extractionId,
    }),
  });
}

export function listInvoices(): Promise<StoredInvoice[]> {
  return request<StoredInvoice[]>("/api/v1/invoices");
}

export function getInvoice(invoiceId: string): Promise<StoredInvoice> {
  return request<StoredInvoice>(`/api/v1/invoices/${invoiceId}`);
}

/** Checks the draft as it is being edited. Nothing is stored. */
export async function checkInvoice(
  rulesetId: string,
  invoice: CanonicalInvoice,
  signal?: AbortSignal,
): Promise<RuleViolation[]> {
  const body = await request<{ violations: RuleViolation[] }>(
    `/api/v1/rulesets/${encodeURIComponent(rulesetId)}/check`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(invoice),
      signal,
    },
  );
  return body.violations;
}

export type ExportResult =
  | { kind: "file"; blob: Blob; fileName: string; warnings: RuleViolation[] }
  | { kind: "incomplete"; violations: RuleViolation[] };

/** Official warnings come with the file in a header, as base64 JSON. */
function readWarnings(response: Response): RuleViolation[] {
  const header = response.headers.get("X-Validation-Warnings");
  if (!header) return [];
  try {
    const bytes = Uint8Array.from(atob(header), (c) => c.charCodeAt(0));
    return JSON.parse(new TextDecoder().decode(bytes)) as RuleViolation[];
  } catch {
    return [];
  }
}

/**
 * Exports the invoice as booked, after the complete official rules. A 422 is
 * not an error: it lists what is missing. A 503 means the official check could
 * not run, so there is no file.
 */
export async function exportInvoice(invoiceId: string, rulesetId: string): Promise<ExportResult> {
  const response = await send(
    `/api/v1/invoices/${invoiceId}/export?ruleset=${encodeURIComponent(rulesetId)}`,
  );

  if (response.status === 422) {
    const body = (await response.json()) as { violations: RuleViolation[] };
    return { kind: "incomplete", violations: body.violations };
  }

  if (!response.ok) throw await failure(response);

  const disposition = response.headers.get("Content-Disposition") ?? "";
  const fileName = /filename="?([^";]+)"?/.exec(disposition)?.[1] ?? `${invoiceId}-${rulesetId}.xml`;
  return { kind: "file", blob: await response.blob(), fileName, warnings: readWarnings(response) };
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

export interface ReceiverLookup {
  status: "found" | "notFound" | "unavailable" | "nothingToLookUp";
  network: string | null;
  participant: { scheme: string; value: string } | null;
  /** xChange formats the receiver takes invoices in. */
  acceptedRulesetIds: string[];
  documentTypeCount: number;
  /** The addresses asked, "0106:12345678", in order of confidence. */
  tried: string[];
}

/** Whether the buyer can receive e-invoices, and in which formats. Looked up by its numbers, never its name. */
export function lookupReceiver(
  buyer: {
    vatIdentifier: string | null;
    legalRegistrationId: string | null;
    legalRegistrationScheme: string | null;
    electronicAddress: string | null;
    electronicAddressScheme: string | null;
    countryCode: string | null;
  },
  signal?: AbortSignal,
): Promise<ReceiverLookup> {
  return request<ReceiverLookup>("/api/v1/receivers/lookup", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(buyer),
    signal,
  });
}
