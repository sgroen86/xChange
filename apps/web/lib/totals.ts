/**
 * Line nets, VAT breakdown and document totals for the review screen.
 *
 * A port of the server's InvoiceTotalsCalculator
 * (src/InvoicePlatform.Domain/Calculation): VAT per category and rate over the
 * summed base, cents, half away from zero. The exported file uses the server's
 * calculation; this one shows the same figures while the user types. Both are
 * tested on the same examples (totals.test.ts and InvoiceTotalsCalculatorTests).
 *
 * Takes a structural input rather than importing the canonical types, so that
 * `node --test` can load it on its own.
 */

import {
  add,
  compare,
  format,
  normalize,
  parseDecimal,
  percentOf,
  round,
  sub,
  sum,
  divRound,
  mul,
  isZero,
  ONE,
  ZERO,
  type Dec,
} from "./decimal.ts";

export interface AdjustmentInput {
  isCharge: boolean;
  amount: string | null;
  vatCategoryCode?: string | null;
  vatPercentage?: string | null;
}

export interface LineInput {
  quantity: string | null;
  unitPrice: string | null;
  baseQuantity: string | null;
  netAmount: string | null;
  vatCategoryCode: string | null;
  vatPercentage: string | null;
  allowancesAndCharges: AdjustmentInput[];
}

export interface TotalsInput {
  lines: LineInput[];
  allowancesAndCharges: AdjustmentInput[];
  totals: { prepaidAmount: string | null; roundingAmount: string | null } | null;
  /** Rate to the tax currency (PINT-SG: SGD); when given, the totals are also converted. */
  taxCurrencyRate?: string | null;
}

export interface VatBucket {
  category: string;
  /** Without trailing zeros, e.g. "21". */
  rate: string;
  taxable: string;
  tax: string;
}

export interface CalculatedTotals {
  /** Per line, in line order; null when the line has no quantity, price or net amount. */
  lineNet: (string | null)[];
  vatBreakdown: VatBucket[];
  lineExtension: string;
  allowanceTotal: string | null;
  chargeTotal: string | null;
  taxExclusive: string;
  tax: string;
  taxInclusive: string;
  prepaid: string | null;
  rounding: string | null;
  payable: string;
  /** Without tax and the tax each rounded to the cent, with tax their sum - as the server does. */
  inTaxCurrency: { exclusive: string; tax: string; inclusive: string } | null;
}

function signed(adjustment: AdjustmentInput): Dec {
  const amount = parseDecimal(adjustment.amount) ?? ZERO;
  return adjustment.isCharge ? amount : sub(ZERO, amount);
}

/** quantity x price / base quantity, rounded, plus charges minus allowances. */
function lineNet(line: LineInput): Dec | null {
  const quantity = parseDecimal(line.quantity);
  const price = parseDecimal(line.unitPrice);
  if (quantity === null || price === null) return parseDecimal(line.netAmount);

  const parsedBase = parseDecimal(line.baseQuantity);
  const base = parsedBase === null || isZero(parsedBase) ? ONE : parsedBase;
  const net = divRound(mul(quantity, price), base, 2);
  return round(add(net, sum(line.allowancesAndCharges.map(signed))), 2);
}

interface Bucket {
  category: string;
  rate: Dec;
  amount: Dec;
}

export function calculateTotals(input: TotalsInput): CalculatedTotals {
  const nets = input.lines.map(lineNet);
  const buckets: Bucket[] = [];

  const addToBucket = (category: string | null | undefined, rateText: string | null | undefined, amount: Dec) => {
    const key = category ?? "";
    const rate = parseDecimal(rateText ?? null) ?? ZERO;
    const existing = buckets.find((b) => b.category === key && compare(b.rate, rate) === 0);
    if (existing) existing.amount = add(existing.amount, amount);
    else buckets.push({ category: key, rate, amount });
  };

  input.lines.forEach((line, index) => addToBucket(line.vatCategoryCode, line.vatPercentage, nets[index] ?? ZERO));
  input.allowancesAndCharges.forEach((a) => addToBucket(a.vatCategoryCode, a.vatPercentage, signed(a)));

  // Category code ordinal, then rate ascending - the server's order.
  buckets.sort((a, b) =>
    a.category < b.category ? -1 : a.category > b.category ? 1 : compare(a.rate, b.rate),
  );

  const breakdown = buckets.map((bucket) => {
    const taxable = round(bucket.amount, 2);
    return { bucket, taxable, tax: percentOf(taxable, bucket.rate) };
  });

  const allowances = input.allowancesAndCharges.filter((a) => !a.isCharge);
  const charges = input.allowancesAndCharges.filter((a) => a.isCharge);

  const lineExtension = round(sum(nets.map((n) => n ?? ZERO)), 2);
  const allowanceTotal = allowances.length === 0 ? null : round(sum(allowances.map((a) => parseDecimal(a.amount) ?? ZERO)), 2);
  const chargeTotal = charges.length === 0 ? null : round(sum(charges.map((a) => parseDecimal(a.amount) ?? ZERO)), 2);

  const taxExclusive = round(add(sub(lineExtension, allowanceTotal ?? ZERO), chargeTotal ?? ZERO), 2);
  const tax = round(sum(breakdown.map((b) => b.tax)), 2);
  const taxInclusive = round(add(taxExclusive, tax), 2);
  const prepaid = parseDecimal(input.totals?.prepaidAmount ?? null);
  const rounding = parseDecimal(input.totals?.roundingAmount ?? null);
  const payable = round(add(sub(taxInclusive, prepaid ?? ZERO), rounding ?? ZERO), 2);

  const rate = parseDecimal(input.taxCurrencyRate ?? null);
  const inTaxCurrency =
    rate === null
      ? null
      : (() => {
          const exclusive = round(mul(taxExclusive, rate), 2);
          const taxConverted = round(mul(tax, rate), 2);
          return {
            exclusive: format(exclusive, 2),
            tax: format(taxConverted, 2),
            inclusive: format(add(exclusive, taxConverted), 2),
          };
        })();

  return {
    lineNet: nets.map((n) => (n === null ? null : format(n, 2))),
    vatBreakdown: breakdown.map(({ bucket, taxable, tax: bucketTax }) => ({
      category: bucket.category,
      rate: normalize(bucket.rate),
      taxable: format(taxable, 2),
      tax: format(bucketTax, 2),
    })),
    lineExtension: format(lineExtension, 2),
    allowanceTotal: allowanceTotal === null ? null : format(allowanceTotal, 2),
    chargeTotal: chargeTotal === null ? null : format(chargeTotal, 2),
    taxExclusive: format(taxExclusive, 2),
    tax: format(tax, 2),
    taxInclusive: format(taxInclusive, 2),
    prepaid: prepaid === null ? null : format(prepaid, 2),
    rounding: rounding === null ? null : format(rounding, 2),
    payable: format(payable, 2),
    inTaxCurrency,
  };
}
