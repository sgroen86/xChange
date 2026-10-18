import { locales, type Lang } from "../content/langs.ts";

/** Replaces {name} placeholders in a dictionary string. */
export function fill(template: string, values: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (match, key: string) =>
    key in values ? String(values[key]) : match,
  );
}

/**
 * Formats integer euro cents for display. Whole euros are shown without
 * decimals ("€ 19"), anything else with two ("€ 1,50"). Euros and cents are
 * split with integer arithmetic so no amount passes through a fraction.
 */
export function formatEuroCents(cents: number, lang: Lang): string {
  if (!Number.isInteger(cents)) throw new Error(`Amount must be whole cents, got ${cents}`);
  const whole = cents % 100 === 0;
  const formatter = new Intl.NumberFormat(locales[lang], {
    style: "currency",
    currency: "EUR",
    minimumFractionDigits: whole ? 0 : 2,
    maximumFractionDigits: whole ? 0 : 2,
  });
  return formatter.format(cents / 100);
}

export function formatNumber(value: number, lang: Lang): string {
  return new Intl.NumberFormat(locales[lang]).format(value);
}

export function formatDate(iso: string, lang: Lang): string {
  const [year, month, day] = iso.split("-").map(Number);
  return new Intl.DateTimeFormat(locales[lang], { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" }).format(
    new Date(Date.UTC(year, month - 1, day)),
  );
}
