/**
 * The buyer is recognised the way the server does it (RelationKeys): VAT
 * number first, then registration number, never the name.
 */

import assert from "node:assert/strict";
import test from "node:test";

import { matchRelation, normalizeVat } from "./relationKeys.ts";

const relations = [
  { name: "Van Dijk", vatIdentifier: "NL814912345B01", legalRegistrationId: "87654321" },
  { name: "Brit Ltd", vatIdentifier: "GB123456789", legalRegistrationId: null },
  { name: "Tan Trading", vatIdentifier: null, legalRegistrationId: "200212345" },
];

test("a VAT number is normalised before it is compared", () => {
  assert.equal(normalizeVat("nl 8149.12.345-b01"), "NL814912345B01");
  assert.equal(matchRelation(relations, { vatIdentifier: "GB 123 4567 89", legalRegistrationId: null })?.name, "Brit Ltd");
});

test("without a VAT match the registration number decides, on its digits", () => {
  const match = matchRelation(relations, { vatIdentifier: "GB000000000", legalRegistrationId: "2002 12345" });
  assert.equal(match?.name, "Tan Trading");
});

test("the VAT number wins over the registration number", () => {
  const match = matchRelation(relations, { vatIdentifier: "GB123456789", legalRegistrationId: "87654321" });
  assert.equal(match?.name, "Brit Ltd");
});

test("a party without either number is not matched", () => {
  assert.equal(matchRelation(relations, { vatIdentifier: null, legalRegistrationId: " " }), null);
  assert.equal(matchRelation(relations, null), null);
});
