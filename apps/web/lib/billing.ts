/**
 * Client for the organisation's plan and credits. Payment itself happens at
 * Stripe: the API hands out a checkout or portal URL and the browser goes
 * there. Coming back changes nothing by itself; the plan updates when Stripe's
 * webhook reaches the API, so the settings page polls for a short while.
 *
 * Amounts arrive as strings and are shown as they are (CLAUDE.md, hard rule 3).
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders, clearSession } from "./auth";
import { text } from "./i18n";

export type PlanId = "starter" | "pro" | "business";

export interface BillingStatus {
  /** Null without a running plan. */
  plan: PlanId | null;
  /** Stripe's status: active, past_due, canceled, ...; null without a subscription. */
  status: string | null;
  periodStart: string | null;
  periodEnd: string | null;
  cancelAtPeriodEnd: boolean;
  /** Conversions this period; null without a plan. */
  periodAllowance: number | null;
  periodUsed: number;
  /** Purchased credits left; used after the month's conversions, never expire. */
  credits: number;
  /** On the trial and never paid: downloading needs a plan or credits. */
  trial: boolean;
  /** The trial's conversions (or a set limit); null: no limit. */
  baseLimit: number | null;
  baseUsed: number;
  /** A Stripe customer exists, so the portal can open. */
  hasCustomer: boolean;
}

export interface BillingPlan {
  id: PlanId;
  name: string;
  monthlyConversions: number;
  /** "19.00", excluding VAT; null when Stripe has no active price. */
  amount: string | null;
  currency: string | null;
}

export interface BillingPlans {
  plans: BillingPlan[];
  credits: { conversions: number; amount: string | null; currency: string | null };
}

async function send<T>(path: string, init?: RequestInit): Promise<T> {
  if (!isApiConfigured) throw new ApiError(text().common.noApi, 0);

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      headers: { ...(init?.headers ?? {}), ...authHeaders() },
    });
  } catch {
    throw new ApiError(text().common.apiUnreachable, 0);
  }

  if (response.status === 401) {
    clearSession();
    throw new ApiError(text().shell.api.sessionExpired, 401);
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as { detail?: string } | null;
    throw new ApiError(problem?.detail ?? text().common.apiStatus(response.status), response.status);
  }

  return (await response.json()) as T;
}

export function getBilling(): Promise<BillingStatus> {
  return send<BillingStatus>("/api/v1/billing");
}

export function getPlans(): Promise<BillingPlans> {
  return send<BillingPlans>("/api/v1/billing/plans");
}

/** Starts Stripe Checkout for a plan or a credit pack, and goes there. */
export async function startCheckout(purchase: { plan: PlanId } | { credits: true }): Promise<void> {
  const { url } = await send<{ url: string }>("/api/v1/billing/checkout", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(purchase),
  });
  window.location.assign(url);
}

/** Opens Stripe's customer portal: change or cancel the plan, card, invoices. */
export async function openPortal(): Promise<void> {
  const { url } = await send<{ url: string }>("/api/v1/billing/portal", { method: "POST" });
  window.location.assign(url);
}
