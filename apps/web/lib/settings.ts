/**
 * Client for the organisation's settings. The organisation comes from the
 * session; only an admin may change the settings, which the API enforces.
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders } from "./auth";

export interface TaxCategoryInfo {
  code: string;
  label: string;
}

export interface RulesetInfo {
  id: string;
  displayName: string;
  rulesRelease: string;
  /** "btw" or "GST", for labels. */
  taxName: string;
  taxCategories: TaxCategoryInfo[];
  /** When set (PINT-SG: SGD), an invoice in another currency needs an exchange rate. */
  taxCurrency: string | null;
}

export interface OrganizationSettings {
  /** Read-only here; only the platform administrator changes them. */
  organizationName?: string | null;
  countryCode?: string | null;
  defaultRulesetId: string | null;
  /** What the export menu opens on: the organisation's choice, else its country's default. */
  effectiveDefaultRulesetId?: string | null;
  endpointId: string | null;
  endpointScheme: string | null;
  /** Every ruleset that can be chosen, the system default first. */
  rulesets: RulesetInfo[];
}

/** Without an API there is one ruleset to show, and nothing to load. */
export const FALLBACK_SETTINGS: OrganizationSettings = {
  defaultRulesetId: null,
  endpointId: null,
  endpointScheme: null,
  rulesets: [
    {
      id: "peppol-bis-billing-3.0",
      displayName: "PEPPOL BIS Billing 3.0 (VK, België, Nederland)",
      rulesRelease: "3.0.21",
      taxName: "btw",
      taxCategories: [
        { code: "S", label: "standaardtarief" },
        { code: "Z", label: "nultarief" },
        { code: "E", label: "vrijgesteld" },
        { code: "AE", label: "btw verlegd" },
        { code: "G", label: "export buiten de EU" },
        { code: "O", label: "niet onderworpen" },
      ],
      taxCurrency: null,
    },
  ],
};

async function call(method: "GET" | "PUT", body?: unknown): Promise<OrganizationSettings> {
  if (!isApiConfigured) return FALLBACK_SETTINGS;

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/v1/organization/settings`, {
      method,
      headers: { "Content-Type": "application/json", ...authHeaders() },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError("De API is niet bereikbaar.", 0);
  }

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    const detail = (payload as { detail?: string } | null)?.detail;
    throw new ApiError(detail ?? `De API gaf status ${response.status}.`, response.status);
  }
  return payload as OrganizationSettings;
}

export function getSettings(): Promise<OrganizationSettings> {
  return call("GET");
}

export function updateSettings(update: {
  defaultRulesetId: string | null;
  endpointId: string | null;
  endpointScheme: string | null;
}): Promise<OrganizationSettings> {
  return call("PUT", update);
}

/** The ruleset the format menu opens on: the organisation's choice, else its country's, else the first. */
export function defaultRulesetOf(settings: OrganizationSettings): RulesetInfo {
  const id = settings.effectiveDefaultRulesetId ?? settings.defaultRulesetId;
  return settings.rulesets.find((r) => r.id === id) ?? settings.rulesets[0];
}
