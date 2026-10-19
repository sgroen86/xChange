/**
 * Display formatting for amounts carried as strings.
 *
 * Amounts never pass through floating point: arithmetic is in lib/decimal.ts
 * (BigInt) and lib/totals.ts, and this only rearranges the digits of a
 * decimal string. See CLAUDE.md, hard rule 3. The language only changes the
 * separators, never the digits.
 */

import { format, parseDecimal } from "./decimal.ts";
import { currentLanguage, type Lang } from "./i18n/language.ts";

/**
 * "1234.5" -> "€ 1.234,50" in Dutch, "€1,234.50" in English. Returns "—" for an
 * absent or malformed value.
 */
export function formatMoney(
  value: string | null | undefined,
  currency: string | null | undefined,
  lang: Lang = currentLanguage(),
): string {
  if (value === null || value === undefined) return "—";
  const trimmed = value.trim();
  if (!/^-?\d+(\.\d+)?$/.test(trimmed)) return "—";

  const negative = trimmed.startsWith("-");
  const [whole, fraction = ""] = (negative ? trimmed.slice(1) : trimmed).split(".");
  const english = lang === "en";
  const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, english ? "," : ".");
  const cents = (fraction + "00").slice(0, Math.max(2, fraction.length));
  const symbol = !currency || currency === "EUR" ? "€" : currency;
  // "€1,234.50" but "USD 1,234.50": a letter code keeps its space.
  const gap = english && symbol === "€" ? "" : " ";
  return `${negative ? "-" : ""}${symbol}${gap}${grouped}${english ? "." : ","}${cents}`;
}

/**
 * A model cost in USD, rounded to `decimals` (half away from zero) by digit
 * arithmetic: "0.012345" -> "$0.0123" in English, "$ 0,0123" in Dutch.
 * Returns "—" for an absent or malformed value.
 */
export function formatUsd(
  value: string | null | undefined,
  lang: Lang = currentLanguage(),
  decimals = 4,
): string {
  const parsed = parseDecimal(value);
  if (!parsed) return "—";

  const fixed = format(parsed, decimals);
  const negative = fixed.startsWith("-");
  const [whole, fraction = ""] = (negative ? fixed.slice(1) : fixed).split(".");
  const english = lang === "en";
  const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, english ? "," : ".");
  const decimalPart = fraction ? `${english ? "." : ","}${fraction}` : "";
  return `${negative ? "-" : ""}$${english ? "" : " "}${grouped}${decimalPart}`;
}
