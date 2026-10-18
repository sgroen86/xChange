import assert from "node:assert/strict";
import { test } from "node:test";

import { formatMoney } from "./money.ts";

test("Dutch separators", () => {
  assert.equal(formatMoney("1234.5", "EUR", "nl"), "€ 1.234,50");
  assert.equal(formatMoney("-1234567.125", "EUR", "nl"), "-€ 1.234.567,125");
  assert.equal(formatMoney("12", "USD", "nl"), "USD 12,00");
});

test("English separators, same digits", () => {
  assert.equal(formatMoney("1234.5", "EUR", "en"), "€1,234.50");
  assert.equal(formatMoney("-1234567.125", "EUR", "en"), "-€1,234,567.125");
  assert.equal(formatMoney("12", "USD", "en"), "USD 12.00");
});

test("nothing to show", () => {
  assert.equal(formatMoney(null, "EUR", "en"), "—");
  assert.equal(formatMoney("1,5", "EUR", "nl"), "—");
});
