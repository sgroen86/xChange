/**
 * Client for the xChange extraction API.
 *
 * The base URL is configuration, not a constant: the frontend is a static
 * export served from greenitsolutions.net while the API runs on AWS, and the
 * two move independently. Set NEXT_PUBLIC_XCHANGE_API_URL at build time.
 *
 * With no base URL configured the app stays on mock data, so a build that has
 * not been pointed at an API still works rather than failing at runtime.
 */

import { authHeaders, clearSession } from "./auth";
import { normalizeInvoice, type ReviewDraft } from "./canonical";

export const API_BASE_URL = (process.env.NEXT_PUBLIC_XCHANGE_API_URL ?? "").replace(/\/$/, "");

export const isApiConfigured = API_BASE_URL.length > 0;

export interface ApiValidationWarning {
  code: string;
  path: string;
  message: string;
  severity: "warning" | "error" | "info";
  extractedValue: string | null;
  calculatedValue: string | null;
}

export interface ApiProviderExecution {
  provider: string;
  model: string;
  durationMs: number;
}

export interface ExtractInvoiceApiResponse {
  invoiceId: string;
  /** The server's CanonicalInvoiceDraft, amounts as strings. Normalised by toDraft. */
  canonicalInvoice: unknown;
  canonicalXml: string;
  warnings: ApiValidationWarning[];
  providerExecution: ApiProviderExecution;
}

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/**
 * POSTs the PDF to the extraction endpoint. The field name is "file", matching
 * the API contract.
 */
export async function extractInvoice(
  file: File,
  signal?: AbortSignal,
): Promise<ExtractInvoiceApiResponse> {
  if (!isApiConfigured) {
    throw new ApiError("Er is geen API geconfigureerd.", 0);
  }

  const body = new FormData();
  body.append("file", file, file.name);

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/v1/invoices/extract`, {
      method: "POST",
      // Content-Type is deliberately not set: the browser must add the
      // multipart boundary itself.
      headers: authHeaders(),
      body,
      signal,
    });
  } catch {
    // A CORS rejection and a dead network are indistinguishable to the page by
    // design, so name both rather than guessing which one it was.
    throw new ApiError(
      "De API is niet bereikbaar. Controleer de verbinding of de CORS-instelling.",
      0,
    );
  }

  if (response.status === 401) {
    // The session expired or was revoked server-side. Drop it so the app asks
    // for a login rather than retrying with a token that will never work.
    clearSession();
    throw new ApiError("Uw sessie is verlopen. Log opnieuw in.", 401);
  }

  if (!response.ok) {
    throw new ApiError(await describeFailure(response), response.status);
  }

  return (await response.json()) as ExtractInvoiceApiResponse;
}

/** Turns an RFC 7807 problem response into something a person can act on. */
async function describeFailure(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { title?: string; detail?: string };
    const text = problem.detail ?? problem.title;
    if (text) return text;
  } catch {
    // Not a problem document; fall through to the status text.
  }

  if (response.status === 413) return "Het bestand is te groot voor de API.";
  if (response.status === 502) return "De extractie is mislukt bij de provider.";
  return `De API gaf status ${response.status}.`;
}

/** The API response as a review draft: the server's model, normalised. */
export function toDraft(
  response: ExtractInvoiceApiResponse,
  fileName: string,
  byteSize: number,
): ReviewDraft {
  return {
    id: response.invoiceId,
    invoice: normalizeInvoice(response.canonicalInvoice),
    source: {
      fileName,
      byteSize,
      engine: `${response.providerExecution.provider}/${response.providerExecution.model}`,
      extractedAt: new Date().toISOString(),
    },
  };
}
