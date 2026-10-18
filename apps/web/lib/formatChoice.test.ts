import assert from "node:assert/strict";
import test from "node:test";

import { chooseFormat } from "./formatChoice.ts";

const base = { chosen: null, customerFormat: null, receiverAccepts: null, organizationDefault: "pint-sg-billing-1" };

test("the choice for this download wins over everything", () => {
  assert.deepEqual(
    chooseFormat({ ...base, chosen: "nlcius-si-ubl-2.0", customerFormat: "peppol-bis-billing-3.0" }),
    { id: "nlcius-si-ubl-2.0", source: "chosen" },
  );
});

test("a format set on the customer wins over what the receiver accepts", () => {
  assert.deepEqual(
    chooseFormat({ ...base, customerFormat: "nlcius-si-ubl-2.0", receiverAccepts: ["peppol-bis-billing-3.0"] }),
    { id: "nlcius-si-ubl-2.0", source: "customer" },
  );
});

test("a UK receiver that only takes BIS 3.0 moves a Singapore organisation off PINT-SG", () => {
  assert.deepEqual(chooseFormat({ ...base, receiverAccepts: ["peppol-bis-billing-3.0"] }), {
    id: "peppol-bis-billing-3.0",
    source: "receiver",
  });
});

test("the organisation's default stays when the receiver accepts it", () => {
  assert.deepEqual(
    chooseFormat({ ...base, receiverAccepts: ["peppol-bis-billing-3.0", "pint-sg-billing-1"] }),
    { id: "pint-sg-billing-1", source: "organization" },
  );
});

test("an unknown or empty receiver changes nothing", () => {
  assert.equal(chooseFormat({ ...base, receiverAccepts: [] }).source, "organization");
  assert.equal(chooseFormat(base).source, "organization");
});
