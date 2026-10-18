/**
 * The same examples, with the same numbers, as the server's
 * tests/InvoicePlatform.Domain.Tests/Calculation/InvoiceTotalsCalculatorTests.cs:
 * the review screen and the exported file must agree to the cent.
 */
import assert from "node:assert/strict";
import { test } from "node:test";

import { calculateTotals, type LineInput, type TotalsInput } from "./totals.ts";

function line(quantity: string, unitPrice: string, category: string, rate: string | null, baseQuantity: string | null = null): LineInput {
  return {
    quantity,
    unitPrice,
    baseQuantity,
    netAmount: null,
    vatCategoryCode: category,
    vatPercentage: rate,
    allowancesAndCharges: [],
  };
}

function invoice(partial: Partial<TotalsInput>): TotalsInput {
  return { lines: [], allowancesAndCharges: [], totals: null, ...partial };
}

test("example 1: two rates, VAT per category not per line", () => {
  const result = calculateTotals(
    invoice({ lines: [line("1", "1250.00", "S", "21"), line("7.5", "89.50", "S", "21"), line("2", "34.95", "S", "9")] }),
  );

  assert.deepEqual(result.lineNet, ["1250.00", "671.25", "69.90"]);
  assert.deepEqual(result.vatBreakdown, [
    { category: "S", rate: "9", taxable: "69.90", tax: "6.29" },
    { category: "S", rate: "21", taxable: "1921.25", tax: "403.46" },
  ]);
  assert.equal(result.lineExtension, "1991.15");
  assert.equal(result.taxExclusive, "1991.15");
  assert.equal(result.tax, "409.75");
  assert.equal(result.taxInclusive, "2400.90");
  assert.equal(result.payable, "2400.90");
  assert.equal(result.allowanceTotal, null);
  assert.equal(result.chargeTotal, null);
});

test("example 2: rounding half away from zero, and a base quantity", () => {
  const result = calculateTotals(invoice({ lines: [line("3", "0.335", "S", "21"), line("10", "12.50", "S", "21", "100")] }));

  assert.deepEqual(result.lineNet, ["1.01", "1.25"]);
  assert.equal(result.taxExclusive, "2.26");
  assert.equal(result.tax, "0.47");
});

test("example 3: allowances and charges on line and document", () => {
  const discounted: LineInput = {
    ...line("1", "100.00", "S", "21"),
    allowancesAndCharges: [{ isCharge: false, amount: "10.00" }],
  };

  const result = calculateTotals(
    invoice({
      lines: [discounted],
      allowancesAndCharges: [
        { isCharge: true, amount: "5.00", vatCategoryCode: "S", vatPercentage: "21" },
        { isCharge: false, amount: "2.50", vatCategoryCode: "S", vatPercentage: "21" },
      ],
      totals: { prepaidAmount: "20.00", roundingAmount: "0.01" },
    }),
  );

  assert.deepEqual(result.lineNet, ["90.00"]);
  assert.equal(result.lineExtension, "90.00");
  assert.equal(result.allowanceTotal, "2.50");
  assert.equal(result.chargeTotal, "5.00");
  assert.equal(result.taxExclusive, "92.50");
  assert.deepEqual(result.vatBreakdown, [{ category: "S", rate: "21", taxable: "92.50", tax: "19.43" }]);
  assert.equal(result.taxInclusive, "111.93");
  assert.equal(result.payable, "91.94");
});

test("example 4: an exempt category has rate 0 and no VAT", () => {
  const result = calculateTotals(invoice({ lines: [line("2", "50.00", "E", null)] }));

  assert.deepEqual(result.vatBreakdown, [{ category: "E", rate: "0", taxable: "100.00", tax: "0.00" }]);
});

test("a line without quantity or price keeps its net amount", () => {
  const result = calculateTotals(
    invoice({
      lines: [{ ...line("", "", "S", "21"), quantity: null, unitPrice: null, netAmount: "42.00" }],
    }),
  );

  assert.equal(result.lineExtension, "42.00");
  assert.equal(result.tax, "8.82");
});

test("rates written differently land in the same bucket", () => {
  const result = calculateTotals(invoice({ lines: [line("1", "10", "S", "21"), line("1", "10", "S", "21.00")] }));

  assert.equal(result.vatBreakdown.length, 1);
  assert.equal(result.vatBreakdown[0].taxable, "20.00");
});

test("an invoice in another currency is also totalled in the tax currency (PINT-SG: SGD)", () => {
  // The server's PintSgBilling1RulesetTests example: 2279.80 USD, GST 205.18, rate 1.3425.
  const result = calculateTotals(
    invoice({
      lines: [line("10", "150.00", "SR", "9"), line("2", "389.90", "SR", "9")],
      taxCurrencyRate: "1.3425",
    }),
  );

  assert.equal(result.taxExclusive, "2279.80");
  assert.equal(result.tax, "205.18");
  assert.deepEqual(result.inTaxCurrency, { exclusive: "3060.63", tax: "275.45", inclusive: "3336.08" });
});
