import assert from "node:assert/strict";
import { test } from "node:test";

import { add, divRound, format, normalize, parseDecimal, percentOf, round } from "./decimal.ts";

const d = (text: string) => {
  const value = parseDecimal(text);
  assert.ok(value, `"${text}" should parse`);
  return value;
};

test("parses plain decimals and nothing else", () => {
  assert.equal(format(d("-12.50")), "-12.50");
  assert.equal(format(d("0007")), "7");
  for (const bad of ["", " ", "1,5", "1e3", "1.", ".5", "€5", "1 000"]) {
    assert.equal(parseDecimal(bad), null, `"${bad}" should not parse`);
  }
  assert.equal(parseDecimal(null), null);
});

test("adds exactly where floating point does not", () => {
  assert.equal(format(add(d("0.1"), d("0.2"))), "0.3");
  assert.equal(format(add(d("9007199254740993.01"), d("0.01"))), "9007199254740993.02");
});

test("rounds half away from zero", () => {
  assert.equal(format(round(d("1.005"), 2)), "1.01");
  assert.equal(format(round(d("-1.005"), 2)), "-1.01");
  assert.equal(format(round(d("1.004"), 2)), "1.00");
  assert.equal(format(round(d("2.5"), 0)), "3");
});

test("divides with rounding", () => {
  assert.equal(format(divRound(d("10"), d("3"), 2)), "3.33");
  assert.equal(format(divRound(d("-2"), d("3"), 2)), "-0.67");
  assert.equal(format(divRound(d("1125.00"), d("100"), 2)), "11.25");
});

test("takes a percentage to the cent", () => {
  assert.equal(format(percentOf(d("92.50"), d("21"))), "19.43"); // 19.425
  assert.equal(format(percentOf(d("2.26"), d("21"))), "0.47"); // 0.4746
});

test("normalises rates for use as a key", () => {
  assert.equal(normalize(d("21.00")), "21");
  assert.equal(normalize(d("9.50")), "9.5");
  assert.equal(normalize(d("0")), "0");
});
