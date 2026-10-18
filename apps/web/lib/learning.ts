/**
 * Client for what xChange learned from the organisation's corrections. The
 * organisation comes from the session.
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders } from "./auth";
import { text, type Dictionary } from "./i18n";

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
  if (!isApiConfigured) throw new ApiError(text().common.noApi, 0);

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers: { ...authHeaders() } });
  } catch {
    throw new ApiError(text().common.apiUnreachable, 0);
  }

  if (!response.ok) throw new ApiError(text().common.apiStatus(response.status), response.status);
  return response;
}

export async function listLearned(): Promise<LearnedCorrection[]> {
  if (!isApiConfigured) return [];
  return (await (await call("/api/v1/learning")).json()) as LearnedCorrection[];
}

export async function forgetLearned(id: string): Promise<void> {
  await call(`/api/v1/learning/${encodeURIComponent(id)}`, { method: "DELETE" });
}

/** A label for a learned field, in the given (default: current) language. */
export function fieldLabel(field: string, t: Dictionary = text()): string {
  const learned = t.settings.learned;
  if (field.startsWith("exemptionReason:")) return learned.exemptionReason(field.slice("exemptionReason:".length));
  return (learned.fields as Record<string, string>)[field] ?? field;
}

/** "S|21" -> "S 21%"; other values unchanged; nothing -> "(leeg)" / "(empty)". */
export function valueLabel(field: string, value: string | null, t: Dictionary = text()): string {
  if (!value) return t.settings.learned.emptyValue;
  if (field !== "lines.tax") return value;
  const [category, rate] = value.split("|");
  return rate ? `${category} ${rate}%` : category;
}
