/**
 * Client for the organisation's relations (its customers). They are created
 * by booking invoices; this only lists and corrects them. The organisation
 * comes from the session.
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders } from "./auth";

export interface Relation {
  relationId: string;
  name: string | null;
  vatIdentifier: string | null;
  legalRegistrationId: string | null;
  legalRegistrationScheme: string | null;
  electronicAddress: string | null;
  electronicAddressScheme: string | null;
  countryCode: string | null;
  /** The export format for invoices to this customer; null means the organisation's. */
  defaultRulesetId: string | null;
  updatedAt: string;
}

export type RelationUpdate = Omit<Relation, "relationId" | "updatedAt">;

async function call<T>(path: string, init?: RequestInit): Promise<T> {
  if (!isApiConfigured) throw new ApiError("Er is geen API geconfigureerd.", 0);

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      headers: { "Content-Type": "application/json", ...authHeaders() },
    });
  } catch {
    throw new ApiError("De API is niet bereikbaar.", 0);
  }

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    const detail = (payload as { detail?: string } | null)?.detail;
    throw new ApiError(detail ?? `De API gaf status ${response.status}.`, response.status);
  }
  return payload as T;
}

export function listRelations(): Promise<Relation[]> {
  return isApiConfigured ? call<Relation[]>("/api/v1/relations") : Promise.resolve([]);
}

export function updateRelation(relationId: string, update: RelationUpdate): Promise<Relation> {
  return call<Relation>(`/api/v1/relations/${encodeURIComponent(relationId)}`, {
    method: "PUT",
    body: JSON.stringify(update),
  });
}
