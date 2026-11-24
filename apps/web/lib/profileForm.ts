/**
 * Pure helpers for the company profile screen. No imports, so node:test can
 * load this file as it is.
 */

export type ProfileTab = "general" | "peppol";

/** First message per field path, e.g. { "general.iban": "…" }. */
export type FieldErrors = Record<string, string>;

/** The first message per field of an ASP.NET validation problem (`errors`), or none. */
export function fieldErrorsOf(payload: unknown): FieldErrors {
  const errors = (payload as { errors?: unknown } | null)?.errors;
  if (!errors || typeof errors !== "object") return {};

  const result: FieldErrors = {};
  for (const [field, messages] of Object.entries(errors as Record<string, unknown>)) {
    if (Array.isArray(messages) && typeof messages[0] === "string") result[field] = messages[0];
  }
  return result;
}

/** The saved legal name, else the name the organisation was created with, so the first save records it. */
export function legalNameToShow(saved: string | null | undefined, organizationName: string | null | undefined): string {
  return saved ?? organizationName ?? "";
}

/** Form text to wire value: trimmed, empty as null. */
export function toWire(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === "" ? null : trimmed;
}

/** The tab a field path is on, so an error on a hidden tab can open it. */
export function tabOf(field: string): ProfileTab {
  return field.startsWith("peppol.") ? "peppol" : "general";
}
