import assert from "node:assert/strict";
import test from "node:test";

import {
  NUDGE_DISMISS_MS,
  dismissNudge,
  isNudgeDismissed,
  knownMfaEnabled,
  rememberMfaEnabled,
  shouldShowNudge,
} from "./mfaNudge.ts";

function memoryStorage() {
  const items = new Map<string, string>();
  return {
    getItem: (key: string) => items.get(key) ?? null,
    setItem: (key: string, value: string) => void items.set(key, value),
    removeItem: (key: string) => void items.delete(key),
    items,
  };
}

const blocked = {
  getItem: (): string | null => {
    throw new Error("blocked");
  },
  setItem: () => {
    throw new Error("blocked");
  },
  removeItem: () => {
    throw new Error("blocked");
  },
};

const now = Date.parse("2026-10-23T09:00:00Z");

test("a dismissal lasts thirty days", () => {
  const storage = memoryStorage();
  assert.equal(isNudgeDismissed(storage, now), false);

  dismissNudge(storage, now);

  assert.equal(isNudgeDismissed(storage, now), true);
  assert.equal(isNudgeDismissed(storage, now + NUDGE_DISMISS_MS - 1), true);
  assert.equal(isNudgeDismissed(storage, now + NUDGE_DISMISS_MS), false);
});

test("a dismissal from the future or an unreadable one does not count", () => {
  const storage = memoryStorage();
  dismissNudge(storage, now + 60_000);
  assert.equal(isNudgeDismissed(storage, now), false);

  storage.items.set("xchange.mfaNudgeDismissedAt", "not a number");
  assert.equal(isNudgeDismissed(storage, now), false);
});

test("blocked storage shows the notice and never throws", () => {
  assert.doesNotThrow(() => dismissNudge(blocked, now));
  assert.equal(isNudgeDismissed(blocked, now), false);
  assert.doesNotThrow(() => rememberMfaEnabled("u1", false, blocked));
  assert.equal(knownMfaEnabled("u1", blocked), null);
  assert.equal(isNudgeDismissed(null, now), false);
});

test("the status is remembered per user for the session", () => {
  const storage = memoryStorage();
  assert.equal(knownMfaEnabled("u1", storage), null);

  rememberMfaEnabled("u1", false, storage);

  assert.equal(knownMfaEnabled("u1", storage), false);
  assert.equal(knownMfaEnabled("u2", storage), null);

  rememberMfaEnabled("u1", true, storage);
  assert.equal(knownMfaEnabled("u1", storage), true);
});

test("the notice shows only when MFA is known to be off and not dismissed", () => {
  assert.equal(shouldShowNudge(false, false), true);
  assert.equal(shouldShowNudge(false, true), false);
  assert.equal(shouldShowNudge(true, false), false);
  assert.equal(shouldShowNudge(null, false), false);
});
