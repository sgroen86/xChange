/**
 * Readable names for the fields the reading-quality measure counts, and how
 * its percentages are shown. The field names are the server's
 * (ReadingComparer.FieldNames in InvoicePlatform.Domain/Quality); labels reuse
 * the review screen's wherever it has one.
 *
 * Free of runtime imports so `node --test` can load it directly.
 */

import type { Dictionary } from "./i18n/index.ts";
import type { Lang } from "./i18n/language.ts";

const PARTY_FIELDS = [
  "name",
  "vatIdentifier",
  "legalRegistrationId",
  "legalRegistrationScheme",
  "electronicAddress",
  "electronicAddressScheme",
  "address.streetName",
  "address.additionalStreetName",
  "address.postalZone",
  "address.cityName",
  "address.countryCode",
] as const;

/** Every field the server can count, in its order. */
export const QUALITY_FIELDS: readonly string[] = [
  "typeCode",
  "invoiceNumber",
  "issueDate",
  "dueDate",
  "currencyCode",
  "buyerReference",
  "purchaseOrderReference",
  "precedingInvoiceReference",
  ...PARTY_FIELDS.map((field) => `seller.${field}`),
  ...PARTY_FIELDS.map((field) => `buyer.${field}`),
  "payment.paymentMeansCode",
  "payment.iban",
  "payment.bic",
  "payment.paymentReference",
  "lines.count",
  "lines[].description",
  "lines[].quantity",
  "lines[].unitPrice",
  "lines[].netAmount",
  "lines[].vatCategoryCode",
  "lines[].vatPercentage",
  "vatBreakdown[].taxableAmount",
  "vatBreakdown[].taxAmount",
  "totals.lineExtensionAmount",
  "totals.taxExclusiveAmount",
  "totals.taxAmount",
  "totals.taxInclusiveAmount",
  "totals.payableAmount",
];

/** A field's name for people, e.g. "Verkoper · Btw-nummer"; the field itself when it is unknown here. */
export function fieldLabel(field: string, t: Dictionary): string {
  const r = t.review;
  const q = t.costs.quality;

  const header: Record<string, string> = {
    typeCode: r.details.type,
    invoiceNumber: r.details.invoiceNumber,
    issueDate: r.details.issueDate,
    dueDate: r.details.dueDate,
    currencyCode: r.details.currency,
    buyerReference: r.details.buyerReference,
    purchaseOrderReference: r.details.purchaseOrder,
    precedingInvoiceReference: r.details.precedingInvoice,
    "lines.count": q.labels.lineCount,
  };
  const party: Record<string, string> = {
    name: r.party.name,
    vatIdentifier: r.party.vatIdentifier,
    legalRegistrationId: r.party.registrationId,
    legalRegistrationScheme: r.party.registrationScheme,
    electronicAddress: r.party.electronicAddress,
    electronicAddressScheme: r.party.electronicAddressScheme,
    "address.streetName": r.party.street,
    "address.additionalStreetName": q.labels.additionalStreet,
    "address.postalZone": r.party.postalCode,
    "address.cityName": r.party.city,
    "address.countryCode": r.party.countryCode,
  };
  const grouped: Record<string, [string, Record<string, string>]> = {
    "seller.": [q.groups.seller, party],
    "buyer.": [q.groups.buyer, party],
    "payment.": [
      q.groups.payment,
      { paymentMeansCode: r.payment.means, iban: r.payment.iban, bic: r.payment.bic, paymentReference: r.payment.reference },
    ],
    "lines[].": [
      q.groups.line,
      {
        description: r.lines.description,
        quantity: r.lines.quantity,
        unitPrice: r.lines.price,
        netAmount: r.lines.net,
        vatCategoryCode: r.lines.taxCategory(r.vat),
        vatPercentage: r.lines.taxPercentage(r.vat),
      },
    ],
    "vatBreakdown[].": [q.groups.vat, { taxableAmount: q.labels.taxableAmount, taxAmount: q.labels.taxAmount }],
    "totals.": [
      q.groups.totals,
      {
        lineExtensionAmount: r.totals.lines,
        taxExclusiveAmount: r.totals.exclusive(r.vat),
        taxAmount: q.labels.totalTax,
        taxInclusiveAmount: q.labels.inclusive,
        payableAmount: r.totals.payable,
      },
    ],
  };

  if (header[field]) return header[field];
  for (const [prefix, [group, labels]] of Object.entries(grouped)) {
    const label = field.startsWith(prefix) ? labels[field.slice(prefix.length)] : undefined;
    if (label) return `${group} · ${label}`;
  }
  return field;
}

/** "80.0" -> "80,0%" in Dutch, "80.0%" in English; "—" when there is nothing to divide. */
export function formatPercent(value: string | null | undefined, lang: Lang): string {
  if (value === null || value === undefined || !/^\d+(\.\d+)?$/.test(value)) return "—";
  return `${lang === "en" ? value : value.replace(".", ",")}%`;
}
