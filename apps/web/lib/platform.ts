/**
 * Client for platform administration: organisations and the invitation for
 * each organisation's first user. The API refuses everyone but the platform
 * administrator.
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders } from "./auth";

export interface PlatformOrganization {
  organizationId: string;
  name: string;
  countryCode: string;
  createdAt: string;
  userCount: number | null;
  invitationPending: boolean;
  pendingInvitationEmail: string | null;
}

export interface Invitee {
  name: string;
  email: string;
}

export interface PlatformResult {
  organization: PlatformOrganization;
  /** Only when an invitation was made. Shown once: the server keeps only its hash. */
  invitationCode: string | null;
}

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

export function listOrganizations(): Promise<PlatformOrganization[]> {
  return call<PlatformOrganization[]>("/api/v1/platform/organizations");
}

export function createOrganization(input: { name: string; countryCode: string; firstUser: Invitee }): Promise<PlatformResult> {
  return call<PlatformResult>("/api/v1/platform/organizations", { method: "POST", body: JSON.stringify(input) });
}

export function updateOrganization(organizationId: string, input: { name: string; countryCode: string }): Promise<PlatformResult> {
  return call<PlatformResult>(`/api/v1/platform/organizations/${encodeURIComponent(organizationId)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function reissueInvitation(organizationId: string, invitee: Invitee): Promise<PlatformResult> {
  return call<PlatformResult>(`/api/v1/platform/organizations/${encodeURIComponent(organizationId)}/invitation`, {
    method: "POST",
    body: JSON.stringify(invitee),
  });
}

/**
 * The link the administrator sends: this site's /invite/ page with the code.
 * Built from the current URL (…/platform/), so it carries whatever base path
 * this build was exported under - production or a staging lane alike.
 */
export function invitationLink(code: string): string {
  const base = window.location.pathname.replace(/\/platform\/?.*$/, "");
  return `${window.location.origin}${base}/invite/?code=${encodeURIComponent(code)}`;
}
