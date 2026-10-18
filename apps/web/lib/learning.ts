/**
 * Client for what xChange learned from the organisation's corrections. The
 * organisation comes from the session.
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders } from "./auth";

export interface LearnedCorrection {
  id: string;
  /** "organization", or "buyer:<VAT or registration number>". */
  scope: string;
  /** A field path, or "lines.tax", "lines.unitCode", "exemptionReason:<category>". */
  field: string;
  from: string | null;
  to: string | null;
  context: string | null;
  /** "apply": xChange puts it back; "hint": the model reads with it. */
  use: "apply" | "hint";
  count: number;
  lastSeen: string;
}

async function call(path: string, init?: RequestInit): Promise<Response> {
  if (!isApiConfigured) throw new ApiError("Er is geen API geconfigureerd.", 0);

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers: { ...authHeaders() } });
  } catch {
    throw new ApiError("De API is niet bereikbaar.", 0);
  }

  if (!response.ok) throw new ApiError(`De API gaf status ${response.status}.`, response.status);
  return response;
}

export async function listLearned(): Promise<LearnedCorrection[]> {
  if (!isApiConfigured) return [];
  return (await (await call("/api/v1/learning")).json()) as LearnedCorrection[];
}

export async function forgetLearned(id: string): Promise<void> {
  await call(`/api/v1/learning/${encodeURIComponent(id)}`, { method: "DELETE" });
}

const LABELS: Record<string, string> = {
  "seller.name": "Naam verkoper",
  "seller.vatIdentifier": "Btw-nummer verkoper",
  "seller.legalRegistrationId": "KvK-nummer verkoper",
  "seller.legalRegistrationScheme": "Schema registratienummer verkoper",
  "seller.address.streetName": "Straat verkoper",
  "seller.address.postalZone": "Postcode verkoper",
  "seller.address.cityName": "Plaats verkoper",
  "seller.address.countryCode": "Land verkoper",
  "payment.paymentMeansCode": "Betaalwijze",
  "payment.iban": "IBAN",
  "payment.bic": "BIC",
  "payment.accountName": "Rekeninghouder",
  buyerReference: "Referentie afnemer",
  purchaseOrderReference: "Inkoopordernummer",
  dueDate: "Vervaldatum",
  typeCode: "Soort factuur",
  currencyCode: "Valuta",
  "lines.unitCode": "Eenheid op een regel",
  "lines.tax": "Btw-categorie en tarief per regel",
};

/** A Dutch label for a learned field. */
export function fieldLabel(field: string): string {
  if (field.startsWith("exemptionReason:")) return `Vrijstellingsreden ${field.slice("exemptionReason:".length)}`;
  return LABELS[field] ?? field;
}

/** "S|21" -> "S 21%"; other values unchanged; nothing -> "(leeg)". */
export function valueLabel(field: string, value: string | null): string {
  if (!value) return "(leeg)";
  if (field !== "lines.tax") return value;
  const [category, rate] = value.split("|");
  return rate ? `${category} ${rate}%` : category;
}
