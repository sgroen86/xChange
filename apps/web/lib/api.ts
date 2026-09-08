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
import type { CanonicalInvoiceDraft } from "./canonical";

export const API_BASE_URL = (process.env.NEXT_PUBLIC_XCHANGE_API_URL ?? "").replace(/\/$/, "");

export const isApiConfigured = API_BASE_URL.length > 0;

export interface ApiValidationWarning {
  code: string;
  path: string;
  message: string;
  severity: "warning" | "error";
  extractedValue: number | null;
  calculatedValue: number | null;
}

export interface ApiProviderExecution {
  provider: string;
  model: string;
  durationMs: number;
}

/** The backend's canonical invoice. Shaped by the C# CanonicalInvoiceDraft. */
export interface ApiCanonicalInvoice {
  typeCode: string | null;
  invoiceNumber: string | null;
  issueDate: string | null;
  dueDate: string | null;
  currencyCode: string | null;
  note: string | null;
  seller: ApiParty | null;
  buyer: ApiParty | null;
  purchaseOrderReference: string | null;
  buyerReference: string | null;
  payment: Record<string, string | null> | null;
  lines: ApiLine[];
  allowancesAndCharges: unknown[];
  vatBreakdown: ApiVatLine[];
  totals: Record<string, number | null> | null;
  evidence: ApiEvidence[];
}

export interface ApiParty {
  name: string | null;
  vatIdentifier: string | null;
  address: {
    streetName: string | null;
    postalZone: string | null;
    cityName: string | null;
    countryCode: string | null;
  } | null;
}

export interface ApiLine {
  lineId: string | null;
  description: string | null;
  quantity: number | null;
  unitCode: string | null;
  unitPrice: number | null;
  netAmount: number | null;
  vatCategoryCode: string | null;
  vatPercentage: number | null;
}

export interface ApiVatLine {
  vatCategoryCode: string | null;
  vatPercentage: number | null;
  taxableAmount: number | null;
  taxAmount: number | null;
}

export interface ApiEvidence {
  field: string;
  confidence: number | null;
  pageNumber: number | null;
  sourceText: string | null;
}

export interface ExtractInvoiceApiResponse {
  invoiceId: string;
  canonicalInvoice: ApiCanonicalInvoice;
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

/** Maps the API response onto the draft shape the review screen already uses. */
export function toDraft(
  response: ExtractInvoiceApiResponse,
  fileName: string,
  byteSize: number,
): CanonicalInvoiceDraft {
  const invoice = response.canonicalInvoice;

  // Amounts arrive as JSON numbers and are re-stringified immediately: the
  // review screen carries them as strings so editing never rounds them.
  const amount = (value: number | null | undefined): string =>
    value === null || value === undefined ? "" : String(value);

  const confidence = response.warnings.length === 0 ? 0.9 : 0.6;

  return {
    id: response.invoiceId,
    typeCode: invoice.typeCode ?? "380",
    number: invoice.invoiceNumber ?? "",
    issueDate: invoice.issueDate ?? "",
    dueDate: invoice.dueDate ?? "",
    currency: invoice.currencyCode ?? "EUR",
    note: invoice.note ?? "",
    supplier: {
      name: invoice.seller?.name ?? "",
      vatId: invoice.seller?.vatIdentifier ?? "",
      street: invoice.seller?.address?.streetName ?? "",
      postalCode: invoice.seller?.address?.postalZone ?? "",
      city: invoice.seller?.address?.cityName ?? "",
      countryCode: invoice.seller?.address?.countryCode ?? "NL",
    },
    customer: {
      name: invoice.buyer?.name ?? "",
      vatId: invoice.buyer?.vatIdentifier ?? "",
      street: invoice.buyer?.address?.streetName ?? "",
      postalCode: invoice.buyer?.address?.postalZone ?? "",
      city: invoice.buyer?.address?.cityName ?? "",
      countryCode: invoice.buyer?.address?.countryCode ?? "NL",
    },
    lines: invoice.lines.map((line, index) => ({
      id: line.lineId ?? `line-${index + 1}`,
      description: line.description ?? "",
      quantity: amount(line.quantity) || "1",
      unitCode: line.unitCode ?? "C62",
      unitPrice: amount(line.unitPrice) || "0.00",
      vatRate: amount(line.vatPercentage) || "0",
    })),
    extraction: {
      sourceFileName: fileName,
      sourceByteSize: byteSize,
      engine: `${response.providerExecution.provider}/${response.providerExecution.model}`,
      confidence,
      extractedAt: new Date().toISOString(),
    },
    validated: false,
  };
}
