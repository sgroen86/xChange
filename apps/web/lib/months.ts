/**
 * Calendar months as "yyyy-MM" text, in UTC - the way the API keys model
 * cost. Integer arithmetic on year and month only; no Date maths across
 * time zones. Deliberately free of imports so `node --test` can load it.
 */

/** The current month in UTC. */
export function currentMonth(now: Date = new Date()): string {
  return now.toISOString().slice(0, 7);
}

/** "2026-01" moved by -1 is "2025-12". */
export function shiftMonth(month: string, delta: number): string {
  const [year, number] = month.split("-").map((part) => parseInt(part, 10));
  const index = year * 12 + (number - 1) + delta;
  const shiftedYear = Math.floor(index / 12);
  const shiftedMonth = (index % 12) + 1;
  return `${String(shiftedYear).padStart(4, "0")}-${String(shiftedMonth).padStart(2, "0")}`;
}

/** Exactly yyyy-MM. */
export function isMonth(text: string): boolean {
  return /^\d{4}-(0[1-9]|1[0-2])$/.test(text);
}
