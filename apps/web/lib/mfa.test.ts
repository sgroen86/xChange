import assert from "node:assert/strict";
import { test } from "node:test";

import { formatSecret, isCompleteCode, normalizeCode, recoveryCodesText, signInStep } from "./mfa.ts";

test("a login answer is a session, a code step or a setup step", () => {
  assert.deepEqual(signInStep({ token: "t" }), { kind: "session" });
  assert.deepEqual(signInStep({ mfaRequired: true, mfaSetupRequired: false, challenge: "c" }), { kind: "mfa", challenge: "c" });
  assert.deepEqual(signInStep({ mfaRequired: false, mfaSetupRequired: true, challenge: "c" }), { kind: "mfaSetup", challenge: "c" });
  // Without a challenge there is nothing to continue with.
  assert.deepEqual(signInStep({ mfaRequired: true }), { kind: "session" });
});

test("codes keep their digits only, at most six", () => {
  assert.equal(normalizeCode(" 123 456 "), "123456");
  assert.equal(normalizeCode("123-4567"), "123456");
  assert.equal(normalizeCode("12a3"), "123");
  assert.ok(isCompleteCode("012345"));
  assert.ok(!isCompleteCode("12345"));
  assert.ok(!isCompleteCode("12345a"));
});

test("the secret is shown in groups of four", () => {
  assert.equal(formatSecret("GEZDGNBVGY3TQOJQ"), "GEZD GNBV GY3T QOJQ");
  assert.equal(formatSecret("ABCDEF"), "ABCD EF");
  assert.equal(formatSecret(""), "");
});

test("the download lists the codes one per line under a heading", () => {
  assert.equal(
    recoveryCodesText("xChange recovery codes", "anna@bakkerij.be", ["aaaa-bbbb-cccc", "dddd-eeee-ffff"]),
    "xChange recovery codes\nanna@bakkerij.be\n\naaaa-bbbb-cccc\ndddd-eeee-ffff\n",
  );
  // Right after sign-up the address is not known on this page: no empty line for it.
  assert.equal(recoveryCodesText("Codes", "", ["aaaa-bbbb-cccc"]), "Codes\n\naaaa-bbbb-cccc\n");
});
