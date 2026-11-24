import assert from "node:assert/strict";
import test from "node:test";

import { fieldErrorsOf, legalNameToShow, tabOf, toWire } from "./profileForm.ts";

test("field errors take the first message per field of a validation problem", () => {
  assert.deepEqual(
    fieldErrorsOf({ title: "x", errors: { "general.iban": ["Dit IBAN klopt niet.", "second"], "peppol.endpointScheme": ["Kies"] } }),
    { "general.iban": "Dit IBAN klopt niet.", "peppol.endpointScheme": "Kies" },
  );
});

test("a body without errors gives none", () => {
  assert.deepEqual(fieldErrorsOf(null), {});
  assert.deepEqual(fieldErrorsOf({ detail: "boom" }), {});
  assert.deepEqual(fieldErrorsOf({ errors: { a: "not an array" } }), {});
});

test("the legal name falls back to the organisation's name", () => {
  assert.equal(legalNameToShow("Groen B.V.", "Groen"), "Groen B.V.");
  assert.equal(legalNameToShow(null, "Groen"), "Groen");
  assert.equal(legalNameToShow(null, undefined), "");
});

test("form text goes over the wire trimmed, empty as null", () => {
  assert.equal(toWire("  NL91 "), "NL91");
  assert.equal(toWire("   "), null);
});

test("a field path names its tab", () => {
  assert.equal(tabOf("peppol.endpointId"), "peppol");
  assert.equal(tabOf("general.iban"), "general");
});
