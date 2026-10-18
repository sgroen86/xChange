/**
 * The canonical invoice in the browser: the same model as the server's
 * CanonicalInvoiceDraft (camelCase), so what the review screen edits is
 * exactly what the API stores and exports. Every amount and quantity is a
 * string, never a JS number (CLAUDE.md, hard rule 3).
 *
 * Field paths ("seller.address.countryCode", "lines[2].vatCategoryCode") are
 * shared with the server's rule violations and are the ids of the review
 * screen's inputs, so a violation can take the user to its field.
 */

import { calculateTotals, type CalculatedTotals } from "./totals";

export type Amount = string | null;

export interface CanonicalAddress {
  streetName: string | null;
  additionalStreetName: string | null;
  postalZone: string | null;
  cityName: string | null;
  countrySubdivision: string | null;
  countryCode: string | null;
}

export interface CanonicalParty {
  name: string | null;
  legalRegistrationId: string | null;
  legalRegistrationScheme: string | null;
  vatIdentifier: string | null;
  taxRegistrationId: string | null;
  electronicAddress: string | null;
  electronicAddressScheme: string | null;
  contactName: string | null;
  contactEmail: string | null;
  contactPhone: string | null;
  address: CanonicalAddress | null;
}

export interface CanonicalPaymentDetails {
  paymentMeansCode: string | null;
  paymentMeansText: string | null;
  iban: string | null;
  bic: string | null;
  accountName: string | null;
  paymentReference: string | null;
}

export interface CanonicalAllowanceCharge {
  isCharge: boolean;
  amount: Amount;
  baseAmount: Amount;
  percentage: Amount;
  reasonCode: string | null;
  reason: string | null;
  vatCategoryCode: string | null;
  vatPercentage: Amount;
}

export interface CanonicalInvoiceLine {
  lineId: string | null;
  description: string | null;
  itemName: string | null;
  sellerItemIdentifier: string | null;
  quantity: Amount;
  unitCode: string | null;
  unitPrice: Amount;
  baseQuantity: Amount;
  netAmount: Amount;
  vatCategoryCode: string | null;
  vatPercentage: Amount;
  allowancesAndCharges: CanonicalAllowanceCharge[];
}

export interface CanonicalVatBreakdownLine {
  vatCategoryCode: string | null;
  vatPercentage: Amount;
  taxableAmount: Amount;
  taxAmount: Amount;
  exemptionReason: string | null;
}

export interface CanonicalTotals {
  lineExtensionAmount: Amount;
  allowanceTotalAmount: Amount;
  chargeTotalAmount: Amount;
  taxExclusiveAmount: Amount;
  taxAmount: Amount;
  taxInclusiveAmount: Amount;
  prepaidAmount: Amount;
  roundingAmount: Amount;
  payableAmount: Amount;
  taxExclusiveAmountInTaxCurrency: Amount;
  taxAmountInTaxCurrency: Amount;
  taxInclusiveAmountInTaxCurrency: Amount;
}

export interface FieldEvidence {
  field: string;
  confidence: Amount;
  pageNumber: number | null;
  sourceText: string | null;
}

export interface CanonicalInvoice {
  typeCode: string | null;
  invoiceNumber: string | null;
  issueDate: string | null;
  dueDate: string | null;
  currencyCode: string | null;
  note: string | null;
  seller: CanonicalParty | null;
  buyer: CanonicalParty | null;
  purchaseOrderReference: string | null;
  buyerReference: string | null;
  precedingInvoiceReference: string | null;
  /** BT-SG-003; filled in by the server on export. */
  documentUuid: string | null;
  /** BT-6; set by the ruleset (PINT-SG: SGD), not by the user. */
  taxCurrencyCode: string | null;
  /** Units of the tax currency per unit of the invoice currency, entered by the user. */
  taxCurrencyRate: Amount;
  payment: CanonicalPaymentDetails | null;
  lines: CanonicalInvoiceLine[];
  allowancesAndCharges: CanonicalAllowanceCharge[];
  vatBreakdown: CanonicalVatBreakdownLine[];
  totals: CanonicalTotals | null;
  evidence: FieldEvidence[];
}

/** What the review screen holds: the invoice plus where it came from. */
export interface ReviewDraft {
  id: string;
  /** The server's id for this upload; booking passes it back so xChange learns from the corrections. */
  extractionId?: string;
  invoice: CanonicalInvoice;
  source: {
    fileName: string;
    byteSize: number;
    engine: string;
    extractedAt: string;
  };
}

export interface LocalIssue {
  path: string;
  message: string;
}

/* --- Normalising ------------------------------------------------------- */

type Raw = Record<string, unknown> | null | undefined;

const text = (value: unknown): string | null => (typeof value === "string" ? value : null);

/** Amounts may still arrive as numbers from an older API; they become strings at once. */
const amount = (value: unknown): Amount =>
  typeof value === "string" ? value : typeof value === "number" ? String(value) : null;

function normalizeAddress(raw: Raw): CanonicalAddress | null {
  if (!raw) return null;
  return {
    streetName: text(raw.streetName),
    additionalStreetName: text(raw.additionalStreetName),
    postalZone: text(raw.postalZone),
    cityName: text(raw.cityName),
    countrySubdivision: text(raw.countrySubdivision),
    countryCode: text(raw.countryCode),
  };
}

function normalizeParty(raw: Raw): CanonicalParty | null {
  if (!raw) return null;
  return {
    name: text(raw.name),
    legalRegistrationId: text(raw.legalRegistrationId),
    legalRegistrationScheme: text(raw.legalRegistrationScheme),
    vatIdentifier: text(raw.vatIdentifier),
    taxRegistrationId: text(raw.taxRegistrationId),
    electronicAddress: text(raw.electronicAddress),
    electronicAddressScheme: text(raw.electronicAddressScheme),
    contactName: text(raw.contactName),
    contactEmail: text(raw.contactEmail),
    contactPhone: text(raw.contactPhone),
    address: normalizeAddress(raw.address as Raw),
  };
}

function normalizeAdjustment(raw: Raw): CanonicalAllowanceCharge {
  return {
    isCharge: raw?.isCharge === true,
    amount: amount(raw?.amount),
    baseAmount: amount(raw?.baseAmount),
    percentage: amount(raw?.percentage),
    reasonCode: text(raw?.reasonCode),
    reason: text(raw?.reason),
    vatCategoryCode: text(raw?.vatCategoryCode),
    vatPercentage: amount(raw?.vatPercentage),
  };
}

const list = (value: unknown): Raw[] => (Array.isArray(value) ? (value as Raw[]) : []);

/**
 * Turns whatever the API sent - a fresh extraction or a stored record, possibly
 * written before a field existed - into a complete invoice, so the screen never
 * reads undefined.
 */
export function normalizeInvoice(value: unknown): CanonicalInvoice {
  const raw = (value ?? {}) as Record<string, unknown>;
  const payment = raw.payment as Raw;
  const totals = raw.totals as Raw;

  return {
    typeCode: text(raw.typeCode),
    invoiceNumber: text(raw.invoiceNumber),
    issueDate: text(raw.issueDate),
    dueDate: text(raw.dueDate),
    currencyCode: text(raw.currencyCode),
    note: text(raw.note),
    seller: normalizeParty(raw.seller as Raw),
    buyer: normalizeParty(raw.buyer as Raw),
    purchaseOrderReference: text(raw.purchaseOrderReference),
    buyerReference: text(raw.buyerReference),
    precedingInvoiceReference: text(raw.precedingInvoiceReference),
    documentUuid: text(raw.documentUuid),
    taxCurrencyCode: text(raw.taxCurrencyCode),
    taxCurrencyRate: amount(raw.taxCurrencyRate),
    payment: payment
      ? {
          paymentMeansCode: text(payment.paymentMeansCode),
          paymentMeansText: text(payment.paymentMeansText),
          iban: text(payment.iban),
          bic: text(payment.bic),
          accountName: text(payment.accountName),
          paymentReference: text(payment.paymentReference),
        }
      : null,
    lines: list(raw.lines).map((line) => ({
      lineId: text(line?.lineId),
      description: text(line?.description),
      itemName: text(line?.itemName),
      sellerItemIdentifier: text(line?.sellerItemIdentifier),
      quantity: amount(line?.quantity),
      unitCode: text(line?.unitCode),
      unitPrice: amount(line?.unitPrice),
      baseQuantity: amount(line?.baseQuantity),
      netAmount: amount(line?.netAmount),
      vatCategoryCode: text(line?.vatCategoryCode),
      vatPercentage: amount(line?.vatPercentage),
      allowancesAndCharges: list(line?.allowancesAndCharges).map(normalizeAdjustment),
    })),
    allowancesAndCharges: list(raw.allowancesAndCharges).map(normalizeAdjustment),
    vatBreakdown: list(raw.vatBreakdown).map((bucket) => ({
      vatCategoryCode: text(bucket?.vatCategoryCode),
      vatPercentage: amount(bucket?.vatPercentage),
      taxableAmount: amount(bucket?.taxableAmount),
      taxAmount: amount(bucket?.taxAmount),
      exemptionReason: text(bucket?.exemptionReason),
    })),
    totals: totals
      ? {
          lineExtensionAmount: amount(totals.lineExtensionAmount),
          allowanceTotalAmount: amount(totals.allowanceTotalAmount),
          chargeTotalAmount: amount(totals.chargeTotalAmount),
          taxExclusiveAmount: amount(totals.taxExclusiveAmount),
          taxAmount: amount(totals.taxAmount),
          taxInclusiveAmount: amount(totals.taxInclusiveAmount),
          prepaidAmount: amount(totals.prepaidAmount),
          roundingAmount: amount(totals.roundingAmount),
          payableAmount: amount(totals.payableAmount),
          taxExclusiveAmountInTaxCurrency: amount(totals.taxExclusiveAmountInTaxCurrency),
          taxAmountInTaxCurrency: amount(totals.taxAmountInTaxCurrency),
          taxInclusiveAmountInTaxCurrency: amount(totals.taxInclusiveAmountInTaxCurrency),
        }
      : null,
    evidence: list(raw.evidence).map((item) => ({
      field: text(item?.field) ?? "",
      confidence: amount(item?.confidence),
      pageNumber: typeof item?.pageNumber === "number" ? item.pageNumber : null,
      sourceText: text(item?.sourceText),
    })),
  };
}

/** A draft in sessionStorage written by an older version of this screen is not a ReviewDraft. */
export function isReviewDraft(value: unknown): value is ReviewDraft {
  const candidate = value as Partial<ReviewDraft> | null;
  return (
    typeof candidate?.id === "string" &&
    typeof candidate.invoice === "object" &&
    candidate.invoice !== null &&
    Array.isArray(candidate.invoice.lines) &&
    typeof candidate.source === "object" &&
    candidate.source !== null
  );
}

/* --- Paths -------------------------------------------------------------- */

type Segment = string | number;

function segments(path: string): Segment[] {
  const result: Segment[] = [];
  for (const match of path.matchAll(/([^.[\]]+)|\[(\d+)\]/g)) {
    result.push(match[2] !== undefined ? Number(match[2]) : match[1]);
  }
  return result;
}

export function getAtPath(invoice: CanonicalInvoice, path: string): string | null {
  let current: unknown = invoice;
  for (const segment of segments(path)) {
    if (current === null || current === undefined) return null;
    current = (current as Record<string | number, unknown>)[segment];
  }
  return typeof current === "string" ? current : null;
}

/**
 * Immutable update by path. Missing objects on the way (an address, a payment
 * block) are created; an empty string is stored as null, because the server
 * reads null as "absent" and "" as a value.
 */
export function setAtPath(invoice: CanonicalInvoice, path: string, value: string | null): CanonicalInvoice {
  const stored = value === null || value.trim() === "" ? null : value;
  const steps = segments(path);

  const write = (node: unknown, index: number): unknown => {
    const segment = steps[index];
    const isLast = index === steps.length - 1;

    if (typeof segment === "number") {
      const array = Array.isArray(node) ? [...node] : [];
      array[segment] = isLast ? stored : write(array[segment], index + 1);
      return array;
    }

    const object = { ...((node as Record<string, unknown> | null) ?? {}) };
    object[segment] = isLast ? stored : write(object[segment], index + 1);
    return object;
  };

  return write(invoice, 0) as CanonicalInvoice;
}

/* --- Calculation -------------------------------------------------------- */

export function totalsOf(invoice: CanonicalInvoice): CalculatedTotals {
  return calculateTotals(invoice);
}

/**
 * The invoice with calculated line nets, VAT breakdown and totals, as the
 * server will export it. Exemption reasons are kept per category and rate.
 */
export function withCalculatedTotals(invoice: CanonicalInvoice): CanonicalInvoice {
  const totals = calculateTotals(invoice);
  const reasonFor = (category: string, rate: string) =>
    invoice.vatBreakdown.find(
      (bucket) =>
        (bucket.vatCategoryCode ?? "") === category && Number(bucket.vatPercentage ?? "0") === Number(rate),
    )?.exemptionReason ?? null;

  return {
    ...invoice,
    lines: invoice.lines.map((line, index) => ({ ...line, netAmount: totals.lineNet[index] })),
    vatBreakdown: totals.vatBreakdown.map((bucket) => ({
      vatCategoryCode: bucket.category,
      vatPercentage: bucket.rate,
      taxableAmount: bucket.taxable,
      taxAmount: bucket.tax,
      exemptionReason: reasonFor(bucket.category, bucket.rate),
    })),
    totals: {
      lineExtensionAmount: totals.lineExtension,
      allowanceTotalAmount: totals.allowanceTotal,
      chargeTotalAmount: totals.chargeTotal,
      taxExclusiveAmount: totals.taxExclusive,
      taxAmount: totals.tax,
      taxInclusiveAmount: totals.taxInclusive,
      prepaidAmount: totals.prepaid,
      roundingAmount: totals.rounding,
      payableAmount: totals.payable,
      taxExclusiveAmountInTaxCurrency: totals.inTaxCurrency?.exclusive ?? null,
      taxAmountInTaxCurrency: totals.inTaxCurrency?.tax ?? null,
      taxInclusiveAmountInTaxCurrency: totals.inTaxCurrency?.inclusive ?? null,
    },
  };
}

/**
 * What the user can change, without what is calculated or recorded about the
 * extraction. Two invoices with the same fingerprint export the same file, so
 * comparing fingerprints tells whether the screen differs from what was booked.
 */
export function editableFingerprint(invoice: CanonicalInvoice): string {
  return JSON.stringify({
    ...invoice,
    lines: invoice.lines.map((line) => ({ ...line, netAmount: null })),
    vatBreakdown: invoice.vatBreakdown
      .filter((bucket) => bucket.exemptionReason)
      .map((bucket) => ({
        category: bucket.vatCategoryCode ?? "",
        rate: Number(bucket.vatPercentage ?? "0"),
        reason: bucket.exemptionReason,
      }))
      .sort((a, b) => (a.category + a.rate).localeCompare(b.category + b.rate)),
    totals: { prepaid: invoice.totals?.prepaidAmount ?? null, rounding: invoice.totals?.roundingAmount ?? null },
    evidence: [],
  });
}

/** Sets the exemption reason of one VAT category and rate, keeping the others. */
export function setExemptionReason(
  invoice: CanonicalInvoice,
  category: string,
  rate: string,
  reason: string,
): CanonicalInvoice {
  const matches = (bucket: CanonicalVatBreakdownLine) =>
    (bucket.vatCategoryCode ?? "") === category && Number(bucket.vatPercentage ?? "0") === Number(rate);
  const value = reason.trim() === "" ? null : reason;

  const exists = invoice.vatBreakdown.some(matches);
  const vatBreakdown = exists
    ? invoice.vatBreakdown.map((bucket) => (matches(bucket) ? { ...bucket, exemptionReason: value } : bucket))
    : [
        ...invoice.vatBreakdown,
        { vatCategoryCode: category, vatPercentage: rate, taxableAmount: null, taxAmount: null, exemptionReason: value },
      ];

  return { ...invoice, vatBreakdown };
}

export function exemptionReasonOf(invoice: CanonicalInvoice, category: string, rate: string): string {
  return (
    invoice.vatBreakdown.find(
      (bucket) => (bucket.vatCategoryCode ?? "") === category && Number(bucket.vatPercentage ?? "0") === Number(rate),
    )?.exemptionReason ?? ""
  );
}

/* --- Local checks ------------------------------------------------------- */

const DECIMAL = /^-?\d+(\.\d+)?$/;

/**
 * Only what has to be right before the server can read the draft at all:
 * every number must be a number. Everything else - missing fields, codes,
 * VAT rules - comes from the server's ruleset check.
 */
export function localIssues(invoice: CanonicalInvoice): LocalIssue[] {
  const issues: LocalIssue[] = [];
  const check = (value: Amount, path: string, message: string) => {
    if (value !== null && !DECIMAL.test(value.trim())) issues.push({ path, message });
  };

  invoice.lines.forEach((line, index) => {
    const n = index + 1;
    check(line.quantity, `lines[${index}].quantity`, `Regel ${n}: aantal is geen getal. Gebruik een punt als decimaalteken.`);
    check(line.unitPrice, `lines[${index}].unitPrice`, `Regel ${n}: prijs is geen getal. Gebruik een punt als decimaalteken.`);
    check(line.vatPercentage, `lines[${index}].vatPercentage`, `Regel ${n}: btw-percentage is geen getal.`);
    check(line.baseQuantity, `lines[${index}].baseQuantity`, `Regel ${n}: basishoeveelheid is geen getal.`);
  });
  check(invoice.taxCurrencyRate, "taxCurrencyRate", "De wisselkoers is geen getal. Gebruik een punt als decimaalteken.");
  check(invoice.totals?.prepaidAmount ?? null, "totals.prepaidAmount", "Vooruitbetaald bedrag is geen getal.");

  return issues;
}

/* --- Canonical XML ------------------------------------------------------ */

const NAMESPACE = "urn:greenitsolutions:xchange:canonical:1.0";

function escapeXml(value: string): string {
  return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

class XmlBuilder {
  private readonly lines: string[] = ['<?xml version="1.0" encoding="utf-8"?>'];
  private depth = 0;

  open(name: string, attributes = ""): void {
    this.lines.push(`${"  ".repeat(this.depth)}<${name}${attributes}>`);
    this.depth++;
  }

  close(name: string): void {
    this.depth--;
    this.lines.push(`${"  ".repeat(this.depth)}</${name}>`);
  }

  /** Absent values are omitted, never defaulted - as in the server's serializer. */
  value(name: string, value: string | null | undefined, attributes = ""): void {
    if (value === null || value === undefined) return;
    this.lines.push(`${"  ".repeat(this.depth)}<${name}${attributes}>${escapeXml(value)}</${name}>`);
  }

  toString(): string {
    return this.lines.join("\n");
  }
}

const attribute = (name: string, value: string | null | undefined) =>
  value ? ` ${name}="${escapeXml(value)}"` : "";

/**
 * xChange's own canonical XML, a port of CanonicalInvoiceXmlSerializer: same
 * elements, same order, values as held. Deterministic, no model involved
 * (CLAUDE.md, hard rule 1). This is not UBL or a PEPPOL document.
 */
export function toCanonicalXml(invoice: CanonicalInvoice): string {
  const xml = new XmlBuilder();
  const currency = invoice.currencyCode;

  xml.open("CanonicalInvoice", ` xmlns="${NAMESPACE}"`);
  xml.value("TypeCode", invoice.typeCode);
  xml.value("InvoiceNumber", invoice.invoiceNumber);
  xml.value("IssueDate", invoice.issueDate);
  xml.value("DueDate", invoice.dueDate);
  xml.value("CurrencyCode", currency);
  xml.value("Note", invoice.note);
  xml.value("PurchaseOrderReference", invoice.purchaseOrderReference);
  xml.value("BuyerReference", invoice.buyerReference);
  xml.value("PrecedingInvoiceReference", invoice.precedingInvoiceReference);

  const party = (name: string, value: CanonicalParty | null) => {
    if (!value) return;
    xml.open(name);
    xml.value("Name", value.name);
    if (value.legalRegistrationId?.trim())
      xml.value("LegalRegistrationId", value.legalRegistrationId, attribute("schemeId", value.legalRegistrationScheme));
    xml.value("VatIdentifier", value.vatIdentifier);
    xml.value("TaxRegistrationId", value.taxRegistrationId);
    if (value.electronicAddress?.trim())
      xml.value("ElectronicAddress", value.electronicAddress, attribute("schemeId", value.electronicAddressScheme));
    if (value.contactName !== null || value.contactEmail !== null || value.contactPhone !== null) {
      xml.open("Contact");
      xml.value("Name", value.contactName);
      xml.value("Email", value.contactEmail);
      xml.value("Phone", value.contactPhone);
      xml.close("Contact");
    }
    if (value.address) {
      xml.open("PostalAddress");
      xml.value("StreetName", value.address.streetName);
      xml.value("AdditionalStreetName", value.address.additionalStreetName);
      xml.value("PostalZone", value.address.postalZone);
      xml.value("CityName", value.address.cityName);
      xml.value("CountrySubdivision", value.address.countrySubdivision);
      xml.value("CountryCode", value.address.countryCode);
      xml.close("PostalAddress");
    }
    xml.close(name);
  };

  party("Seller", invoice.seller);
  party("Buyer", invoice.buyer);

  if (invoice.payment) {
    xml.open("Payment");
    xml.value("PaymentMeansCode", invoice.payment.paymentMeansCode);
    xml.value("PaymentMeansText", invoice.payment.paymentMeansText);
    xml.value("Iban", invoice.payment.iban);
    xml.value("Bic", invoice.payment.bic);
    xml.value("AccountName", invoice.payment.accountName);
    xml.value("PaymentReference", invoice.payment.paymentReference);
    xml.close("Payment");
  }

  const money = (name: string, value: Amount, withCurrency: boolean) =>
    xml.value(name, value, withCurrency ? attribute("currencyId", currency) : "");

  const adjustments = (items: CanonicalAllowanceCharge[], withCurrency: boolean) => {
    if (items.length === 0) return;
    xml.open("AllowancesAndCharges");
    for (const item of items) {
      const name = item.isCharge ? "Charge" : "Allowance";
      xml.open(name);
      money("Amount", item.amount, withCurrency);
      money("BaseAmount", item.baseAmount, withCurrency);
      xml.value("Percentage", item.percentage);
      xml.value("ReasonCode", item.reasonCode);
      xml.value("Reason", item.reason);
      xml.value("VatCategoryCode", item.vatCategoryCode);
      xml.value("VatPercentage", item.vatPercentage);
      xml.close(name);
    }
    xml.close("AllowancesAndCharges");
  };

  if (invoice.lines.length > 0) {
    xml.open("Lines");
    invoice.lines.forEach((line, index) => {
      xml.open("InvoiceLine");
      xml.value("Id", line.lineId ?? String(index + 1));
      xml.value("Description", line.description);
      xml.value("ItemName", line.itemName);
      xml.value("SellerItemIdentifier", line.sellerItemIdentifier);
      xml.value("Quantity", line.quantity, attribute("unitCode", line.unitCode));
      money("UnitPrice", line.unitPrice, true);
      xml.value("BaseQuantity", line.baseQuantity);
      money("NetAmount", line.netAmount, true);
      xml.value("VatCategoryCode", line.vatCategoryCode);
      xml.value("VatPercentage", line.vatPercentage);
      adjustments(line.allowancesAndCharges, true);
      xml.close("InvoiceLine");
    });
    xml.close("Lines");
  }

  adjustments(invoice.allowancesAndCharges, false);

  if (invoice.vatBreakdown.length > 0) {
    xml.open("VatBreakdown");
    for (const bucket of invoice.vatBreakdown) {
      xml.open("VatBreakdownLine");
      xml.value("VatCategoryCode", bucket.vatCategoryCode);
      xml.value("VatPercentage", bucket.vatPercentage);
      xml.value("TaxableAmount", bucket.taxableAmount);
      xml.value("TaxAmount", bucket.taxAmount);
      xml.value("ExemptionReason", bucket.exemptionReason);
      xml.close("VatBreakdownLine");
    }
    xml.close("VatBreakdown");
  }

  if (invoice.totals) {
    const t = invoice.totals;
    xml.open("Totals");
    xml.value("LineExtensionAmount", t.lineExtensionAmount);
    xml.value("AllowanceTotalAmount", t.allowanceTotalAmount);
    xml.value("ChargeTotalAmount", t.chargeTotalAmount);
    xml.value("TaxExclusiveAmount", t.taxExclusiveAmount);
    xml.value("TaxAmount", t.taxAmount);
    xml.value("TaxInclusiveAmount", t.taxInclusiveAmount);
    xml.value("PrepaidAmount", t.prepaidAmount);
    xml.value("RoundingAmount", t.roundingAmount);
    xml.value("PayableAmount", t.payableAmount);
    xml.close("Totals");
  }

  xml.close("CanonicalInvoice");
  return xml.toString() + "\n";
}

/* --- New invoices and mock data ---------------------------------------- */

export function emptyParty(): CanonicalParty {
  return normalizeParty({}) as CanonicalParty;
}

export function emptyLine(): CanonicalInvoiceLine {
  return {
    lineId: null,
    description: null,
    itemName: null,
    sellerItemIdentifier: null,
    quantity: "1",
    unitCode: "C62",
    unitPrice: "0.00",
    baseQuantity: null,
    netAmount: null,
    vatCategoryCode: "S",
    vatPercentage: "21",
    allowancesAndCharges: [],
  };
}

/**
 * The fallback when no API is configured. Derives a plausible invoice from the
 * file name only; it does not read the PDF. The organisation is the seller.
 */
export function mockDraft(invoiceId: string, fileName: string, byteSize: number): ReviewDraft {
  const seed = Array.from(fileName).reduce((acc, char) => acc + char.charCodeAt(0), 0);
  const numberFromName = fileName.match(/\d{4,}/)?.[0] ?? String(100000 + (seed % 899999));

  const invoice = normalizeInvoice({
    typeCode: "380",
    invoiceNumber: `F-${numberFromName}`,
    issueDate: "2026-08-14",
    dueDate: "2026-09-13",
    currencyCode: "EUR",
    note: "Automatisch geïnterpreteerd concept. Controleer de velden.",
    buyerReference: "PO-2026-17",
    seller: {
      name: "Green IT Solutions B.V.",
      vatIdentifier: "NL857231409B01",
      address: { streetName: "Keizersgracht 62", postalZone: "1015 CS", cityName: "Amsterdam", countryCode: "NL" },
    },
    buyer: {
      name: "Van Dijk Techniek B.V.",
      vatIdentifier: "NL814912345B01",
      address: { streetName: "Industrieweg 24", postalZone: "3542 AD", cityName: "Utrecht", countryCode: "NL" },
    },
    payment: { paymentMeansCode: "30", iban: "NL91ABNA0417164300" },
    lines: [
      { description: "Onderhoudscontract serverruimte", quantity: "1", unitCode: "C62", unitPrice: "1250.00", vatCategoryCode: "S", vatPercentage: "21" },
      { description: "Installatie-uren senior technicus", quantity: "7.5", unitCode: "HUR", unitPrice: "89.50", vatCategoryCode: "S", vatPercentage: "21" },
      { description: "Vervangende UPS-accu (transport)", quantity: "2", unitCode: "C62", unitPrice: "34.95", vatCategoryCode: "S", vatPercentage: "9" },
    ],
  });

  return {
    id: invoiceId,
    invoice,
    source: { fileName, byteSize, engine: "mock-extractor/0.1", extractedAt: "2026-09-08T10:00:00Z" },
  };
}
