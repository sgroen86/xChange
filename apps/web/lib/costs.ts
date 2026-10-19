/**
 * Client for model cost: what reading PDFs cost per month, across the platform
 * and per organisation, and how well the model read what reviewers booked.
 * The API refuses everyone but the platform administrator; customers see
 * their conversions, never model cost.
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

/** Fields counted, and how they came out. */
export interface OutcomeCounts {
  counted: number;
  correct: number;
  /** The model had a value; the reviewer booked another. */
  corrected: number;
  /** The model left it empty; the reviewer filled it. */
  missed: number;
  /** The model filled it; the reviewer emptied it. */
  extra: number;
}

export interface QualityCounts {
  invoices: number;
  perfectInvoices: number;
  fields: OutcomeCounts;
  /** 0-100 to one decimal, as a decimal string; null when nothing was counted. */
  fieldAccuracyPercent: string | null;
  perfectPercent: string | null;
}

/** The model's own reading, and what the reviewer was shown (after learning and filling). */
export interface QualitySums {
  read: QualityCounts;
  shown: QualityCounts;
}

/** A model or an organisation. */
export interface QualityRow {
  key: string;
  name: string | null;
  sums: QualitySums;
  /** USD, for a model row. */
  costUsd: string | null;
  /** USD per invoice the model read perfectly; null when none. */
  costPerPerfectInvoiceUsd: string | null;
}

export interface FieldQuality {
  /** e.g. "seller.vatIdentifier" or "lines[].netAmount"; see lib/qualityFields.ts. */
  field: string;
  read: OutcomeCounts;
  shown: OutcomeCounts;
}

export interface PlatformQuality {
  month: string;
  total: QualitySums;
  /** All model cost of the month, booked or not. */
  costUsd: string;
  costPerPerfectInvoiceUsd: string | null;
  models: QualityRow[];
  organizations: QualityRow[];
  /** Fields that went wrong at least once, most often wrong first. */
  fields: FieldQuality[];
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

/** How well the model read the invoices booked in a month. */
export async function getPlatformQuality(month: string): Promise<PlatformQuality> {
  return (await (await send(`/api/v1/platform/costs/quality?month=${encodeURIComponent(month)}`)).json()) as PlatformQuality;
}

/** The month as CSV, one row per organisation. */
export async function downloadCostsCsv(month: string): Promise<{ blob: Blob; fileName: string }> {
  const response = await send(`/api/v1/platform/costs/export?month=${encodeURIComponent(month)}`);
  const disposition = response.headers.get("Content-Disposition") ?? "";
  const fileName = /filename="?([^";]+)"?/.exec(disposition)?.[1] ?? `xchange-costs-${month}.csv`;
  return { blob: await response.blob(), fileName };
}
