import assert from "node:assert/strict";
import test from "node:test";

import {
  REMEMBER_FOR_MS,
  SIGNUP_PLANS,
  checkoutPurchase,
  forgetPlan,
  parseLang,
  parsePlan,
  rememberPlan,
  rememberedPlan,
} from "./signupPlan.ts";

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

test("the plans the website links to are recognised", () => {
  assert.equal(parsePlan("starter"), "starter");
  assert.equal(parsePlan("pro"), "pro");
  assert.equal(parsePlan("business"), "business");
  assert.equal(parsePlan("credits"), "credits");
  assert.equal(parsePlan(" Pro "), "pro");
});

test("an unknown or missing plan is ignored", () => {
  assert.equal(parsePlan("enterprise"), null);
  assert.equal(parsePlan("toString"), null);
  assert.equal(parsePlan("__proto__"), null);
  assert.equal(parsePlan(""), null);
  assert.equal(parsePlan(null), null);
  assert.equal(parsePlan(undefined), null);
});

test("only the app's languages are taken from the URL", () => {
  assert.equal(parseLang("nl"), "nl");
  assert.equal(parseLang("EN"), "en");
  assert.equal(parseLang("fr"), null);
  assert.equal(parseLang(null), null);
});

test("a plan checks out as a plan, the credit pack as credits", () => {
  assert.deepEqual(checkoutPurchase("pro"), { plan: "pro" });
  assert.deepEqual(checkoutPurchase("credits"), { credits: true });
});

test("the shown amounts are strings, the credit pack is one-off", () => {
  for (const plan of Object.values(SIGNUP_PLANS)) assert.match(plan.amount, /^\d+\.\d{2}$/);
  assert.equal(SIGNUP_PLANS.credits.oneOff, true);
  assert.equal(SIGNUP_PLANS.starter.oneOff, false);
});

test("a remembered plan comes back until it is forgotten", () => {
  const storage = memoryStorage();
  assert.equal(rememberedPlan(storage, 1000), null);
  rememberPlan("business", storage, 1000);
  assert.equal(rememberedPlan(storage, 2000), "business");
  forgetPlan(storage);
  assert.equal(rememberedPlan(storage, 2000), null);
});

test("a remembered plan expires after the confirmation link would have", () => {
  const storage = memoryStorage();
  rememberPlan("pro", storage, 0);
  assert.equal(rememberedPlan(storage, REMEMBER_FOR_MS), "pro");
  assert.equal(rememberedPlan(storage, REMEMBER_FOR_MS + 1), null);
});

test("damaged or foreign storage contents are ignored", () => {
  const storage = memoryStorage();
  storage.items.set("xchange.signupPlan", "not json");
  assert.equal(rememberedPlan(storage, 0), null);
  storage.items.set("xchange.signupPlan", JSON.stringify({ plan: "enterprise", at: 0 }));
  assert.equal(rememberedPlan(storage, 0), null);
  storage.items.set("xchange.signupPlan", JSON.stringify({ plan: "pro" }));
  assert.equal(rememberedPlan(storage, 0), null);
});

test("blocked or missing storage never throws", () => {
  assert.doesNotThrow(() => rememberPlan("pro", blocked));
  assert.equal(rememberedPlan(blocked), null);
  assert.doesNotThrow(() => forgetPlan(blocked));
  assert.doesNotThrow(() => rememberPlan("pro", null));
  assert.equal(rememberedPlan(null), null);
});
