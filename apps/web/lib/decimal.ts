/**
 * Exact decimal arithmetic on BigInt, for amounts carried as strings.
 *
 * A value is integer units at a scale: "12.50" is { units: 1250n, scale: 2 }.
 * Nothing passes through a JS number, so there is no float rounding and no
 * 2^53 ceiling (CLAUDE.md, hard rule 3). Rounding is half away from zero,
 * like the server's MidpointRounding.AwayFromZero.
 *
 * Deliberately free of imports so `node --test` can load it directly.
 * BigInt() is called rather than written as 10n because the project targets
 * ES2017, where BigInt literals are not allowed.
 */

export interface Dec {
  units: bigint;
  scale: number;
}

const TEN = BigInt(10);
const ZERO_UNITS = BigInt(0);

export const ZERO: Dec = { units: ZERO_UNITS, scale: 0 };
export const ONE: Dec = { units: BigInt(1), scale: 0 };
export const HUNDRED: Dec = { units: BigInt(100), scale: 0 };

function pow10(exponent: number): bigint {
  let result = BigInt(1);
  for (let i = 0; i < exponent; i++) result *= TEN;
  return result;
}

/** "-12.50" -> Dec. Null for anything else: no exponent, no comma, no thousands separator. */
export function parseDecimal(text: string | null | undefined): Dec | null {
  if (text === null || text === undefined) return null;
  const trimmed = text.trim();
  if (!/^-?\d+(\.\d+)?$/.test(trimmed)) return null;

  const negative = trimmed.startsWith("-");
  const [whole, fraction = ""] = (negative ? trimmed.slice(1) : trimmed).split(".");
  const units = BigInt(whole + fraction);
  return { units: negative ? -units : units, scale: fraction.length };
}

function rescaleUp(value: Dec, scale: number): bigint {
  return value.units * pow10(scale - value.scale);
}

export function add(a: Dec, b: Dec): Dec {
  const scale = Math.max(a.scale, b.scale);
  return { units: rescaleUp(a, scale) + rescaleUp(b, scale), scale };
}

export function sub(a: Dec, b: Dec): Dec {
  return add(a, negate(b));
}

export function negate(a: Dec): Dec {
  return { units: -a.units, scale: a.scale };
}

export function mul(a: Dec, b: Dec): Dec {
  return { units: a.units * b.units, scale: a.scale + b.scale };
}

/** a / b rounded to `scale` decimals, half away from zero. */
export function divRound(a: Dec, b: Dec, scale: number): Dec {
  if (b.units === ZERO_UNITS) throw new RangeError("Division by zero");

  // a/b = (a.units / 10^a.scale) / (b.units / 10^b.scale); scaled up by 10^scale.
  let numerator = a.units * pow10(scale + b.scale);
  let denominator = b.units * pow10(a.scale);
  if (denominator < ZERO_UNITS) {
    numerator = -numerator;
    denominator = -denominator;
  }

  const quotient = numerator / denominator; // truncates toward zero
  const remainder = numerator % denominator;
  const twice = (remainder < ZERO_UNITS ? -remainder : remainder) * BigInt(2);

  if (twice >= denominator) {
    return { units: quotient + (numerator < ZERO_UNITS ? BigInt(-1) : BigInt(1)), scale };
  }
  return { units: quotient, scale };
}

export function round(a: Dec, scale: number): Dec {
  return a.scale <= scale ? { units: rescaleUp(a, scale), scale } : divRound(a, ONE, scale);
}

/** amount x rate / 100, rounded to cents. */
export function percentOf(amount: Dec, rate: Dec): Dec {
  return divRound(mul(amount, rate), HUNDRED, 2);
}

export function sum(values: Dec[]): Dec {
  return values.reduce(add, ZERO);
}

export function compare(a: Dec, b: Dec): number {
  const scale = Math.max(a.scale, b.scale);
  const left = rescaleUp(a, scale);
  const right = rescaleUp(b, scale);
  return left < right ? -1 : left > right ? 1 : 0;
}

export function isZero(a: Dec): boolean {
  return a.units === ZERO_UNITS;
}

/** Fixed-point text at the value's own scale, or at `scale` after rounding. */
export function format(a: Dec, scale?: number): string {
  const value = scale === undefined ? a : round(a, scale);
  const negative = value.units < ZERO_UNITS;
  const digits = (negative ? -value.units : value.units).toString().padStart(value.scale + 1, "0");
  const whole = digits.slice(0, digits.length - value.scale);
  const fraction = value.scale > 0 ? "." + digits.slice(digits.length - value.scale) : "";
  return (negative ? "-" : "") + whole + fraction;
}

/** Without trailing zeros: "21.00" -> "21", "9.50" -> "9.5". Used to key VAT rates. */
export function normalize(a: Dec): string {
  let units = a.units;
  let scale = a.scale;
  while (scale > 0 && units % TEN === ZERO_UNITS) {
    units /= TEN;
    scale--;
  }
  return format({ units, scale });
}
