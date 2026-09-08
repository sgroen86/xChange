/**
 * Integer-scaled decimal arithmetic.
 *
 * Amounts never pass through floating point: every value is carried as a string
 * at the edges and as scaled integer units internally, mirroring
 * Bookkeeping's app/Xhange/Support/Decimal.php. See CLAUDE.md, hard rule 3.
 */

export const SCALE_AMOUNT = 2;
export const SCALE_UNIT = 4;

/** Parse a decimal string into integer units at `scale`. Returns null if unparseable. */
export function parseScaled(value: string, scale: number): number | null {
  const trimmed = (value ?? "").trim();
  if (trimmed === "") return null;
  if (!/^-?\d+(\.\d+)?$/.test(trimmed)) return null;

  const negative = trimmed.startsWith("-");
  const unsigned = negative ? trimmed.slice(1) : trimmed;
  const [whole, fraction = ""] = unsigned.split(".");

  const padded = (fraction + "0".repeat(scale)).slice(0, scale);
  const roundUp = fraction.length > scale && Number(fraction[scale]) >= 5;

  let units = Number(whole) * 10 ** scale + Number(padded || "0");
  if (roundUp) units += 1;
  return negative ? -units : units;
}

/** Render integer units at `scale` back to a fixed-point string. */
export function formatScaled(units: number, scale: number): string {
  const negative = units < 0;
  const abs = Math.abs(units).toString().padStart(scale + 1, "0");
  const whole = abs.slice(0, abs.length - scale);
  const fraction = scale > 0 ? "." + abs.slice(abs.length - scale) : "";
  return (negative ? "-" : "") + whole + fraction;
}

/** Rescale integer units, rounding half away from zero. */
export function rescale(units: number, fromScale: number, toScale: number): number {
  if (fromScale === toScale) return units;
  if (toScale > fromScale) return units * 10 ** (toScale - fromScale);

  const divisor = 10 ** (fromScale - toScale);
  const sign = units < 0 ? -1 : 1;
  const abs = Math.abs(units);
  return sign * Math.floor((abs + divisor / 2) / divisor);
}

/** quantity (SCALE_UNIT) x unitPrice (SCALE_UNIT) -> amount (SCALE_AMOUNT). */
export function multiplyToAmount(quantityUnits: number, priceUnits: number): number {
  return rescale(quantityUnits * priceUnits, SCALE_UNIT * 2, SCALE_AMOUNT);
}

/**
 * amount x percent / 100 -> amount, all at SCALE_AMOUNT.
 *
 * amountUnits x percentUnits carries scale 4 and an extra factor of 100 from the
 * percentage, so the result divides by 10^4. Rounds half away from zero.
 */
export function percentOfAmount(amountUnits: number, percentUnits: number): number {
  const product = amountUnits * percentUnits;
  const divisor = 10 ** (SCALE_AMOUNT * 2);
  const sign = product < 0 ? -1 : 1;
  const abs = Math.abs(product);
  return sign * Math.floor((abs + divisor / 2) / divisor);
}

export function sum(values: number[]): number {
  return values.reduce((total, value) => total + value, 0);
}

/** Format scaled units for display, e.g. "1.234,56" for nl-NL. */
export function formatAmount(units: number, currency: string): string {
  const value = formatScaled(units, SCALE_AMOUNT);
  const [whole, fraction] = value.split(".");
  const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
  return `${currency === "EUR" ? "€" : currency} ${grouped},${fraction}`;
}
