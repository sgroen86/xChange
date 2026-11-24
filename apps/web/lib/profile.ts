/**
 * Client for the organisation's company profile: the seller's details on
 * every invoice, with a block per format (PEPPOL first). The organisation
 * comes from the session; only an admin may change it, which the API enforces.
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders } from "./auth";
import { text } from "./i18n";
import { fieldErrorsOf, type FieldErrors } from "./profileForm";

export interface CompanyDetails {
  legalName: string | null;
  vatIdentifier: string | null;
  legalRegistrationId: string | null;
  legalRegistrationScheme: string | null;
  streetName: string | null;
  additionalStreetName: string | null;
  postalZone: string | null;
  cityName: string | null;
  contactName: string | null;
  contactEmail: string | null;
  contactPhone: string | null;
  paymentMeansCode: string | null;
  iban: string | null;
  bic: string | null;
  accountName: string | null;
}

export interface PeppolDetails {
  endpointId: string | null;
  endpointScheme: string | null;
}

export interface OrganizationProfile {
  /** Read-only: the name the organisation was created with. */
  organizationName?: string | null;
  /** Read-only: only the platform administrator changes it. */
  countryCode?: string | null;
  general: CompanyDetails;
  peppol: PeppolDetails;
}

/** A refused save, with the message per field the API gave. */
export class ProfileSaveError extends ApiError {
  constructor(
    message: string,
    status: number,
    readonly fieldErrors: FieldErrors,
  ) {
    super(message, status);
    this.name = "ProfileSaveError";
  }
}

/** Without an API there is nothing to load: an empty profile. */
export const FALLBACK_PROFILE: OrganizationProfile = {
  general: {
    legalName: null,
    vatIdentifier: null,
    legalRegistrationId: null,
    legalRegistrationScheme: null,
    streetName: null,
    additionalStreetName: null,
    postalZone: null,
    cityName: null,
    contactName: null,
    contactEmail: null,
    contactPhone: null,
    paymentMeansCode: null,
    iban: null,
    bic: null,
    accountName: null,
  },
  peppol: { endpointId: null, endpointScheme: null },
};

async function call(method: "GET" | "PUT", body?: unknown): Promise<OrganizationProfile> {
  if (!isApiConfigured) return FALLBACK_PROFILE;

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/v1/organization/profile`, {
      method,
      headers: { "Content-Type": "application/json", ...authHeaders() },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(text().common.apiUnreachable, 0);
  }

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    const problem = payload as { title?: string; detail?: string } | null;
    const message = problem?.detail ?? problem?.title ?? text().common.apiStatus(response.status);
    throw new ProfileSaveError(message, response.status, fieldErrorsOf(payload));
  }
  return payload as OrganizationProfile;
}

export function getProfile(): Promise<OrganizationProfile> {
  return call("GET");
}

export function updateProfile(update: { general: CompanyDetails; peppol: PeppolDetails }): Promise<OrganizationProfile> {
  return call("PUT", update);
}
