/**
 * The plan someone chose on the website before signing up. The site links to
 * /signup/?plan=pro&lang=en; the choice is remembered in this browser until the
 * confirmation link has been opened and the new admin is signed in, and then
 * Checkout starts for it from the settings page.
 *
 * Remembered in localStorage rather than sessionStorage: the confirmation link
 * is opened from a mail, in a new tab, which starts with an empty
 * sessionStorage. The choice expires with the link (24 hours) plus a margin.
 * In another browser nothing is carried over: the user starts on the trial and
 * can choose under Settings.
 *
 * Pure apart from the storage handed in, so it is tested without a browser.
 */

import type { Lang } from "./i18n/language.ts";

export type SignupPlanId = "starter" | "pro" | "business" | "credits";

export interface SignupPlan {
  id: SignupPlanId;
  /** Null for the credit pack: its name is a text ("20 conversions"). */
  name: string | null;
  /** Excluding VAT, as a string (CLAUDE.md, hard rule 3). Shown only: Checkout charges Stripe's price. */
  amount: string;
  currency: "EUR";
  conversions: number;
  /** A one-off credit pack rather than a monthly plan. */
  oneOff: boolean;
}

/**
 * What the sign-up form shows for each choice. Conversions and names match
 * Application/Billing/BillingCatalogue.cs; the amounts match the website's
 * price list (apps/site/content/pricing.ts). The prices endpoint needs a
 * session, so they cannot be read from Stripe before the account exists.
 */
export const SIGNUP_PLANS: Record<SignupPlanId, SignupPlan> = {
  starter: { id: "starter", name: "Starter", amount: "19.00", currency: "EUR", conversions: 50, oneOff: false },
  pro: { id: "pro", name: "Pro", amount: "49.00", currency: "EUR", conversions: 250, oneOff: false },
  business: { id: "business", name: "Business", amount: "129.00", currency: "EUR", conversions: 1000, oneOff: false },
  credits: { id: "credits", name: null, amount: "30.00", currency: "EUR", conversions: 20, oneOff: true },
};

/** A plan id from the URL or storage; anything unknown is ignored. */
export function parsePlan(value: string | null | undefined): SignupPlanId | null {
  const id = value?.trim().toLowerCase() ?? "";
  return Object.prototype.hasOwnProperty.call(SIGNUP_PLANS, id) ? (id as SignupPlanId) : null;
}

/** A supported app language from the URL; anything else (fr, ...) is ignored. */
export function parseLang(value: string | null | undefined): Lang | null {
  const lang = value?.trim().toLowerCase();
  return lang === "nl" || lang === "en" ? lang : null;
}

/** What to ask the billing API for: a plan, or the credit pack. */
export function checkoutPurchase(plan: SignupPlanId): { plan: Exclude<SignupPlanId, "credits"> } | { credits: true } {
  return plan === "credits" ? { credits: true } : { plan };
}

type PlanStorage = Pick<Storage, "getItem" | "setItem" | "removeItem">;

const STORAGE_KEY = "xchange.signupPlan";

/** How long a remembered choice lasts: the confirmation link's 24 hours, plus a margin. */
export const REMEMBER_FOR_MS = 48 * 60 * 60 * 1000;

function browserStorage(): PlanStorage | null {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    return null;
  }
}

export function rememberPlan(plan: SignupPlanId, storage = browserStorage(), now = Date.now()): void {
  try {
    storage?.setItem(STORAGE_KEY, JSON.stringify({ plan, at: now }));
  } catch {
    // Storage blocked: the user lands on the trial and can choose under Settings.
  }
}

/** The remembered choice, or null when there is none, it expired or storage is blocked. */
export function rememberedPlan(storage = browserStorage(), now = Date.now()): SignupPlanId | null {
  try {
    const raw = storage?.getItem(STORAGE_KEY);
    if (!raw) return null;
    const stored = JSON.parse(raw) as { plan?: unknown; at?: unknown };
    const plan = parsePlan(typeof stored.plan === "string" ? stored.plan : null);
    const fresh = typeof stored.at === "number" && now - stored.at >= 0 && now - stored.at <= REMEMBER_FOR_MS;
    return plan && fresh ? plan : null;
  } catch {
    return null;
  }
}

export function forgetPlan(storage = browserStorage()): void {
  try {
    storage?.removeItem(STORAGE_KEY);
  } catch {
    // Nothing to do: it expires by itself.
  }
}
