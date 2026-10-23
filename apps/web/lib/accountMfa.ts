/**
 * Two-step verification from the settings, for the signed-in user. Turning it
 * off is refused by the API for platform administrators, for whom it is
 * mandatory; for everyone else it is optional (owner's decision, 2026-10-23).
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders, type MfaSetupInfo } from "./auth";
import { text } from "./i18n";

export interface MfaStatus {
  enabled: boolean;
  /** Platform administrators only: it cannot be turned off. */
  mandatory: boolean;
  recoveryCodesLeft: number;
}

async function call<T>(method: "GET" | "POST", path: string, body?: unknown): Promise<T> {
  if (!isApiConfigured) throw new ApiError(text().common.noApi, 0);

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/v1/account/mfa${path}`, {
      method,
      headers: { "Content-Type": "application/json", ...authHeaders() },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(text().common.apiUnreachable, 0);
  }

  if (response.status === 204) return undefined as T;

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    const detail = (payload as { detail?: string } | null)?.detail;
    throw new ApiError(detail ?? text().common.apiStatus(response.status), response.status);
  }
  return payload as T;
}

export function getMfaStatus(): Promise<MfaStatus> {
  return call<MfaStatus>("GET", "");
}

export function startAccountMfaSetup(): Promise<MfaSetupInfo> {
  return call<MfaSetupInfo>("POST", "/setup", {});
}

export async function confirmAccountMfa(code: string): Promise<string[]> {
  return (await call<{ recoveryCodes: string[] }>("POST", "/confirm", { code })).recoveryCodes;
}

/** A code from the app or a recovery code. */
export async function regenerateRecoveryCodes(code: string): Promise<string[]> {
  return (await call<{ recoveryCodes: string[] }>("POST", "/recovery-codes", { code })).recoveryCodes;
}

/** A code from the app or a recovery code. */
export function disableMfa(code: string): Promise<void> {
  return call<void>("POST", "/disable", { code });
}
