import assert from "node:assert/strict";
import { test } from "node:test";

import { currentMonth, isMonth, shiftMonth } from "./months.ts";

test("the current month is taken in UTC", () => {
  assert.equal(currentMonth(new Date("2026-10-31T23:30:00-02:00")), "2026-11");
});

test("months move across years", () => {
  assert.equal(shiftMonth("2026-01", -1), "2025-12");
  assert.equal(shiftMonth("2025-12", 1), "2026-01");
  assert.equal(shiftMonth("2026-10", 0), "2026-10");
});

test("a month is exactly yyyy-MM", () => {
  assert.ok(isMonth("2026-10"));
  assert.ok(!isMonth("2026-13"));
  assert.ok(!isMonth("2026-1"));
});
