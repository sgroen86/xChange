/**
 * Display formatting for amounts carried as strings.
 *
 * Amounts never pass through floating point: arithmetic is in lib/decimal.ts
 * (BigInt) and lib/totals.ts, and this only rearranges the digits of a
 * decimal string. See CLAUDE.md, hard rule 3.
 */

/** "1234.5" -> "€ 1.234,50" (nl-NL grouping). Returns "—" for an absent or malformed value. */
export function formatMoney(value: string | null | undefined, currency: string | null | undefined): string {
  if (value === null || value === undefined) return "—";
  const trimmed = value.trim();
  if (!/^-?\d+(\.\d+)?$/.test(trimmed)) return "—";

  const negative = trimmed.startsWith("-");
  const [whole, fraction = ""] = (negative ? trimmed.slice(1) : trimmed).split(".");
  const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
  const cents = (fraction + "00").slice(0, Math.max(2, fraction.length));
  const symbol = !currency || currency === "EUR" ? "€" : currency;
  return `${negative ? "-" : ""}${symbol} ${grouped},${cents}`;
}
