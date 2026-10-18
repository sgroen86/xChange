/**
 * Pricing — PROPOSAL VALUES ONLY.
 *
 * These numbers are a proposal; the owner sets the final prices. Every amount
 * on the site renders from this file, so changing a price here changes it
 * everywhere. Amounts are integer euro cents (no floating point), excluding
 * VAT. Plan descriptions per language live in content/i18n/*.ts under
 * `pricing.plans`, keyed by plan id.
 */

export type PlanId = "starter" | "pro" | "business";

export type Plan = {
  id: PlanId;
  /** Shown as-is in every language. */
  name: string;
  /** Monthly price in euro cents, excluding VAT. */
  monthlyPriceCents: number;
  /** PDF conversions included per calendar month. */
  includedConversions: number;
  /** Rendered as "most popular". */
  highlighted: boolean;
};

export type CreditPack = {
  conversions: number;
  /** Price of the whole pack in euro cents, excluding VAT. */
  priceCents: number;
};

export const pricing = {
  currency: "EUR",
  /** Free trial: conversions included, no payment card needed. */
  trialConversions: 10,
  plans: [
    { id: "starter", name: "Starter", monthlyPriceCents: 1900, includedConversions: 50, highlighted: false },
    { id: "pro", name: "Pro", monthlyPriceCents: 4900, includedConversions: 250, highlighted: true },
    { id: "business", name: "Business", monthlyPriceCents: 12900, includedConversions: 1000, highlighted: false },
  ] satisfies Plan[],
  /** Pay as you go: prepaid credits, no subscription. */
  creditPacks: [{ conversions: 20, priceCents: 3000 }] satisfies CreditPack[],
} as const;

/** Price of a single conversion in a pack, in whole cents (rounded half up). */
export function pricePerConversionCents(pack: CreditPack): number {
  return Math.floor((pack.priceCents * 2 + pack.conversions) / (pack.conversions * 2));
}
