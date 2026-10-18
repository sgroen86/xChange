/**
 * How the review screen recognises the buyer as a known relation - the same
 * keys as the server's RelationKeys: by VAT number, otherwise by registration
 * number, never by name. Pure, so it is tested without a browser.
 */

import type { Relation } from "./relations";

/** "nl 8149.12.345-b01" -> "NL814912345B01". Null when nothing is left. */
export function normalizeVat(vat: string | null | undefined): string | null {
  const normalized = (vat ?? "").replace(/[^\p{L}\p{N}]/gu, "").toUpperCase();
  return normalized.length === 0 ? null : normalized;
}

/** "1234 5678" -> "12345678": registration numbers compare on their digits. */
export function normalizeRegistration(id: string | null | undefined): string | null {
  const digits = (id ?? "").replace(/[^0-9]/g, "");
  return digits.length === 0 ? null : digits;
}

type Party = { vatIdentifier: string | null; legalRegistrationId: string | null } | null;

/** The relation with the party's VAT number, else with its registration number, else null. */
export function matchRelation<T extends Pick<Relation, "vatIdentifier" | "legalRegistrationId">>(
  relations: readonly T[],
  party: Party,
): T | null {
  if (!party) return null;

  const vat = normalizeVat(party.vatIdentifier);
  const byVat = vat === null ? undefined : relations.find((r) => r.vatIdentifier === vat);
  if (byVat) return byVat;

  const registration = normalizeRegistration(party.legalRegistrationId);
  return registration === null
    ? null
    : (relations.find((r) => normalizeRegistration(r.legalRegistrationId) === registration) ?? null);
}
