import assert from "node:assert/strict";
import { test } from "node:test";

import { dictionaries } from "./i18n/index.ts";
import { fieldLabel, formatPercent, QUALITY_FIELDS } from "./qualityFields.ts";

test("every counted field has a readable name in every language", () => {
  for (const [lang, dictionary] of Object.entries(dictionaries)) {
    for (const field of QUALITY_FIELDS) {
      assert.notEqual(fieldLabel(field, dictionary), field, `${lang}: ${field} has no label`);
    }
  }
});

test("labels say which part of the invoice a field belongs to", () => {
  assert.equal(fieldLabel("seller.vatIdentifier", dictionaries.nl), "Verkoper · Btw-nummer");
  assert.equal(fieldLabel("lines[].netAmount", dictionaries.en), "Line · Net amount");
  assert.equal(fieldLabel("invoiceNumber", dictionaries.nl), "Factuurnummer");
  assert.equal(fieldLabel("something.new", dictionaries.nl), "something.new");
});

test("percentages keep the server's digits and the language's decimal sign", () => {
  assert.equal(formatPercent("80.0", "nl"), "80,0%");
  assert.equal(formatPercent("66.7", "en"), "66.7%");
  assert.equal(formatPercent(null, "nl"), "—");
});
