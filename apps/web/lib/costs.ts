/**
 * Client for model cost: what reading PDFs cost per month, across the platform
 * and per organisation. The API refuses everyone but the platform
 * administrator; customers see their conversions, never model cost.
 *
 * Amounts arrive as strings and stay strings (CLAUDE.md, hard rule 3): show
 * them with formatUsd from lib/money.ts, never through Number().
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders } from "./auth";
import { text } from "./i18n";

export interface CostTotals {
  calls: number;
  failedCalls: number;
  inputTokens: number;
  outputTokens: number;
  cacheWriteTokens: number;
  cacheReadTokens: number;
  /** USD, as a decimal string. */
  costUsd: string;
  /** USD per call, failed calls included. */
  averageCostUsd: string;
}

/** A day (yyyy-MM-dd), a model id or an organisation id, with its sums. */
export interface CostRow {
  key: string;
  /** The organisation's name, for an organisation row. */
  name: string | null;
  totals: CostTotals;
}

export interface PlatformCosts {
  month: string;
  priceVersion: string;
  total: CostTotals;
  days: CostRow[];
  models: CostRow[];
  organizations: CostRow[];
}

export interface CostActor {
  /** A user id, "email", or "apikey:<id>". */
  actor: string;
  name: string | null;
  email: string | null;
  totals: CostTotals;
}

export interface CostEvent {
  eventId: string;
  at: string;
  actor: string;
  actorName: string | null;
  source: string;
  /** An extraction, not an invoice: there is no page to link to. */
  extractionId: string;
  model: string;
  inputTokens: number;
  outputTokens: number;
  cacheWriteTokens: number;
  cacheReadTokens: number;
  costUsd: string;
  priceVersion: string;
  priceKnown: boolean;
  succeeded: boolean;
}

export interface OrganizationCosts {
  month: string;
  organizationId: string;
  name: string | null;
  total: CostTotals;
  users: CostActor[];
  models: CostRow[];
  topEvents: CostEvent[];
}

async function send(path: string): Promise<Response> {
  if (!isApiConfigured) throw new ApiError(text().common.noApi, 0);

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, { headers: authHeaders() });
  } catch {
    throw new ApiError(text().common.apiUnreachable, 0);
  }

  if (!response.ok) {
    const payload = await response.json().catch(() => null);
    const detail = (payload as { detail?: string } | null)?.detail;
    throw new ApiError(detail ?? text().common.apiStatus(response.status), response.status);
  }
  return response;
}

export async function getPlatformCosts(month: string): Promise<PlatformCosts> {
  return (await (await send(`/api/v1/platform/costs?month=${encodeURIComponent(month)}`)).json()) as PlatformCosts;
}

export async function getOrganizationCosts(organizationId: string, month: string): Promise<OrganizationCosts> {
  const path = `/api/v1/platform/costs/organizations/${encodeURIComponent(organizationId)}?month=${encodeURIComponent(month)}`;
  return (await (await send(path)).json()) as OrganizationCosts;
}

/** The month as CSV, one row per organisation. */
export async function downloadCostsCsv(month: string): Promise<{ blob: Blob; fileName: string }> {
  const response = await send(`/api/v1/platform/costs/export?month=${encodeURIComponent(month)}`);
  const disposition = response.headers.get("Content-Disposition") ?? "";
  const fileName = /filename="?([^";]+)"?/.exec(disposition)?.[1] ?? `xchange-costs-${month}.csv`;
  return { blob: await response.blob(), fileName };
}
