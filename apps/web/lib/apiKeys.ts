/**
 * Client for the organisation's API keys, which let another system (an ERP,
 * a script) use the conversion API. Only an admin manages them; the
 * organisation comes from the session.
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders } from "./auth";

export interface ApiKeyItem {
  keyId: string;
  name: string;
  /** The first characters of the key, to recognise it by. */
  prefix: string;
  createdAt: string;
  lastUsedAt: string | null;
}

/** A key just made: the only time the key itself is ever sent. */
export interface CreatedApiKey extends ApiKeyItem {
  key: string;
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

export async function listApiKeys(): Promise<ApiKeyItem[]> {
  if (!isApiConfigured) return [];
  return (await (await call("/api/v1/organization/api-keys")).json()) as ApiKeyItem[];
}

export async function createApiKey(name: string): Promise<CreatedApiKey> {
  const response = await call("/api/v1/organization/api-keys", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ name }),
  });
  return (await response.json()) as CreatedApiKey;
}

export async function revokeApiKey(keyId: string): Promise<void> {
  await call(`/api/v1/organization/api-keys/${encodeURIComponent(keyId)}`, { method: "DELETE" });
}
