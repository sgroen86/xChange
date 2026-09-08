/**
 * Canonical invoice model for the prototype.
 *
 * Field names follow Bookkeeping's app/Xhange/Canonical/* so the two stay
 * comparable. This is a mock: nothing here calls OCR, an LLM, AWS or PEPPOL.
 */

import {
  SCALE_AMOUNT,
  SCALE_UNIT,
  formatScaled,
  multiplyToAmount,
  parseScaled,
  percentOfAmount,
  sum,
} from "./money";

export interface CanonicalParty {
  name: string;
  vatId: string;
  street: string;
  postalCode: string;
  city: string;
  countryCode: string;
}

export interface CanonicalLine {
  id: string;
  description: string;
  quantity: string;
  unitCode: string;
  unitPrice: string;
  vatRate: string;
}

export interface ExtractionMeta {
  sourceFileName: string;
  sourceByteSize: number;
  engine: string;
  confidence: number;
  extractedAt: string;
}

export interface CanonicalInvoiceDraft {
  id: string;
  typeCode: string;
  number: string;
  issueDate: string;
  dueDate: string;
  currency: string;
  note: string;
  supplier: CanonicalParty;
  customer: CanonicalParty;
  lines: CanonicalLine[];
  extraction: ExtractionMeta;
  validated: boolean;
}

export interface VatBucket {
  rate: string;
  taxableUnits: number;
  taxUnits: number;
}

export interface CalculationResult {
  lineNetUnits: Record<string, number>;
  vatBreakdown: VatBucket[];
  taxExclusiveUnits: number;
  taxUnits: number;
  taxInclusiveUnits: number;
}

export interface ValidationIssue {
  severity: "error" | "warning";
  code: string;
  path: string;
  message: string;
}

const EMPTY_PARTY: CanonicalParty = {
  name: "",
  vatId: "",
  street: "",
  postalCode: "",
  city: "",
  countryCode: "NL",
};

/**
 * Deterministic totals. Same draft in, same numbers out — no model in the path.
 * See CLAUDE.md, hard rule 2.
 */
export function calculate(draft: CanonicalInvoiceDraft): CalculationResult {
  const lineNetUnits: Record<string, number> = {};
  const buckets = new Map<string, VatBucket>();

  for (const line of draft.lines) {
    const quantityUnits = parseScaled(line.quantity, SCALE_UNIT) ?? 0;
    const priceUnits = parseScaled(line.unitPrice, SCALE_UNIT) ?? 0;
    const netUnits = multiplyToAmount(quantityUnits, priceUnits);
    lineNetUnits[line.id] = netUnits;

    const rateUnits = parseScaled(line.vatRate, SCALE_AMOUNT) ?? 0;
    const key = formatScaled(rateUnits, SCALE_AMOUNT);
    const bucket = buckets.get(key) ?? { rate: key, taxableUnits: 0, taxUnits: 0 };
    bucket.taxableUnits += netUnits;
    buckets.set(key, bucket);
  }

  // VAT is computed per bucket, not per line, so rounding happens once per rate.
  const vatBreakdown = Array.from(buckets.values())
    .map((bucket) => ({
      ...bucket,
      taxUnits: percentOfAmount(bucket.taxableUnits, parseScaled(bucket.rate, SCALE_AMOUNT) ?? 0),
    }))
    .sort((a, b) => Number(a.rate) - Number(b.rate));

  const taxExclusiveUnits = sum(vatBreakdown.map((b) => b.taxableUnits));
  const taxUnits = sum(vatBreakdown.map((b) => b.taxUnits));

  return {
    lineNetUnits,
    vatBreakdown,
    taxExclusiveUnits,
    taxUnits,
    taxInclusiveUnits: taxExclusiveUnits + taxUnits,
  };
}

export function validate(draft: CanonicalInvoiceDraft): ValidationIssue[] {
  const issues: ValidationIssue[] = [];
  const result = calculate(draft);

  if (!draft.number.trim()) {
    issues.push({
      severity: "error",
      code: "BR-02",
      path: "number",
      message: "Factuurnummer ontbreekt.",
    });
  }
  if (!draft.issueDate.trim()) {
    issues.push({
      severity: "error",
      code: "BR-03",
      path: "issueDate",
      message: "Factuurdatum ontbreekt.",
    });
  }
  if (!draft.supplier.name.trim()) {
    issues.push({
      severity: "error",
      code: "BR-06",
      path: "supplier.name",
      message: "Naam van de leverancier ontbreekt.",
    });
  }
  if (!draft.customer.name.trim()) {
    issues.push({
      severity: "error",
      code: "BR-07",
      path: "customer.name",
      message: "Naam van de afnemer ontbreekt.",
    });
  }
  if (draft.lines.length === 0) {
    issues.push({
      severity: "error",
      code: "BR-16",
      path: "lines",
      message: "Een factuur moet minimaal één regel bevatten.",
    });
  }

  if (!draft.supplier.vatId.trim()) {
    issues.push({
      severity: "warning",
      code: "BR-CO-09",
      path: "supplier.vatId",
      message: "Btw-nummer van de leverancier ontbreekt.",
    });
  }

  if (draft.dueDate && draft.issueDate && draft.dueDate < draft.issueDate) {
    issues.push({
      severity: "error",
      code: "BR-CO-25",
      path: "dueDate",
      message: "Vervaldatum ligt vóór de factuurdatum.",
    });
  }

  draft.lines.forEach((line, index) => {
    if (!line.description.trim()) {
      issues.push({
        severity: "warning",
        code: "BR-25",
        path: `lines[${index}].description`,
        message: `Regel ${index + 1} heeft geen omschrijving.`,
      });
    }
    if (parseScaled(line.quantity, SCALE_UNIT) === null) {
      issues.push({
        severity: "error",
        code: "BR-22",
        path: `lines[${index}].quantity`,
        message: `Regel ${index + 1}: aantal is geen geldig getal.`,
      });
    }
    if (parseScaled(line.unitPrice, SCALE_UNIT) === null) {
      issues.push({
        severity: "error",
        code: "BR-24",
        path: `lines[${index}].unitPrice`,
        message: `Regel ${index + 1}: prijs is geen geldig getal.`,
      });
    }
    if (parseScaled(line.vatRate, SCALE_AMOUNT) === null) {
      issues.push({
        severity: "error",
        code: "BR-48",
        path: `lines[${index}].vatRate`,
        message: `Regel ${index + 1}: btw-percentage is geen geldig getal.`,
      });
    }
  });

  if (result.taxInclusiveUnits <= 0 && draft.lines.length > 0) {
    issues.push({
      severity: "warning",
      code: "BR-CO-15",
      path: "totals.taxInclusiveAmount",
      message: "Het totaalbedrag is nul of negatief.",
    });
  }

  if (draft.extraction.confidence < 0.8) {
    issues.push({
      severity: "warning",
      code: "XC-CONF",
      path: "extraction.confidence",
      message: `Lage extractiebetrouwbaarheid (${Math.round(
        draft.extraction.confidence * 100,
      )}%). Controleer de velden zorgvuldig.`,
    });
  }

  return issues;
}

function escapeXml(value: string): string {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function partyXml(tag: string, party: CanonicalParty, indent: string): string {
  return [
    `${indent}<${tag}>`,
    `${indent}  <Name>${escapeXml(party.name)}</Name>`,
    `${indent}  <VatIdentifier>${escapeXml(party.vatId)}</VatIdentifier>`,
    `${indent}  <PostalAddress>`,
    `${indent}    <StreetName>${escapeXml(party.street)}</StreetName>`,
    `${indent}    <PostalZone>${escapeXml(party.postalCode)}</PostalZone>`,
    `${indent}    <CityName>${escapeXml(party.city)}</CityName>`,
    `${indent}    <CountryCode>${escapeXml(party.countryCode)}</CountryCode>`,
    `${indent}  </PostalAddress>`,
    `${indent}</${tag}>`,
  ].join("\n");
}

/**
 * Deterministic XML serialisation. No LLM is involved in producing this
 * document — see CLAUDE.md, hard rule 1. This is xChange's own canonical
 * format, not UBL or a PEPPOL BIS document.
 */
export function toCanonicalXml(draft: CanonicalInvoiceDraft): string {
  const result = calculate(draft);
  const currency = draft.currency || "EUR";
  const amount = (units: number) => formatScaled(units, SCALE_AMOUNT);

  const lines = draft.lines.map((line, index) => {
    const net = result.lineNetUnits[line.id] ?? 0;
    return [
      `    <InvoiceLine>`,
      `      <Id>${index + 1}</Id>`,
      `      <Description>${escapeXml(line.description)}</Description>`,
      `      <Quantity unitCode="${escapeXml(line.unitCode || "C62")}">${escapeXml(
        line.quantity,
      )}</Quantity>`,
      `      <UnitPrice currencyId="${currency}">${escapeXml(line.unitPrice)}</UnitPrice>`,
      `      <VatRate>${escapeXml(line.vatRate)}</VatRate>`,
      `      <NetAmount currencyId="${currency}">${amount(net)}</NetAmount>`,
      `    </InvoiceLine>`,
    ].join("\n");
  });

  const vatLines = result.vatBreakdown.map((bucket) =>
    [
      `    <VatBreakdownLine>`,
      `      <Rate>${bucket.rate}</Rate>`,
      `      <TaxableAmount currencyId="${currency}">${amount(bucket.taxableUnits)}</TaxableAmount>`,
      `      <TaxAmount currencyId="${currency}">${amount(bucket.taxUnits)}</TaxAmount>`,
      `    </VatBreakdownLine>`,
    ].join("\n"),
  );

  return [
    `<?xml version="1.0" encoding="UTF-8"?>`,
    `<CanonicalInvoice xmlns="urn:greenitsolutions:xchange:canonical:1.0">`,
    `  <TypeCode>${escapeXml(draft.typeCode)}</TypeCode>`,
    `  <Number>${escapeXml(draft.number)}</Number>`,
    `  <IssueDate>${escapeXml(draft.issueDate)}</IssueDate>`,
    `  <DueDate>${escapeXml(draft.dueDate)}</DueDate>`,
    `  <Currency>${escapeXml(currency)}</Currency>`,
    `  <Note>${escapeXml(draft.note)}</Note>`,
    partyXml("Supplier", draft.supplier, "  "),
    partyXml("Customer", draft.customer, "  "),
    `  <Lines>`,
    ...lines,
    `  </Lines>`,
    `  <VatBreakdown>`,
    ...vatLines,
    `  </VatBreakdown>`,
    `  <Totals>`,
    `    <TaxExclusiveAmount currencyId="${currency}">${amount(
      result.taxExclusiveUnits,
    )}</TaxExclusiveAmount>`,
    `    <TaxAmount currencyId="${currency}">${amount(result.taxUnits)}</TaxAmount>`,
    `    <TaxInclusiveAmount currencyId="${currency}">${amount(
      result.taxInclusiveUnits,
    )}</TaxInclusiveAmount>`,
    `  </Totals>`,
    `</CanonicalInvoice>`,
    ``,
  ].join("\n");
}

/**
 * Mock interpretation step. Derives a plausible draft from the file name only —
 * it does not read the PDF. Replace with the real extraction pipeline later.
 */
export function mockDraftFromFile(
  invoiceId: string,
  fileName: string,
  byteSize: number,
): CanonicalInvoiceDraft {
  const seed = Array.from(fileName).reduce((acc, char) => acc + char.charCodeAt(0), 0);
  const numberFromName = fileName.match(/\d{4,}/)?.[0] ?? String(100000 + (seed % 899999));

  return {
    id: invoiceId,
    typeCode: "380",
    number: `F-${numberFromName}`,
    issueDate: "2026-08-14",
    dueDate: "2026-09-13",
    currency: "EUR",
    note: "Automatisch geïnterpreteerd concept. Controleer de velden vóór verzending.",
    supplier: {
      ...EMPTY_PARTY,
      name: "Van Dijk Techniek B.V.",
      vatId: "NL814912345B01",
      street: "Industrieweg 24",
      postalCode: "3542 AD",
      city: "Utrecht",
      countryCode: "NL",
    },
    customer: {
      ...EMPTY_PARTY,
      name: "Green IT Solutions B.V.",
      vatId: "NL857231409B01",
      street: "Keizersgracht 62",
      postalCode: "1015 CS",
      city: "Amsterdam",
      countryCode: "NL",
    },
    lines: [
      {
        id: "line-1",
        description: "Onderhoudscontract serverruimte",
        quantity: "1",
        unitCode: "C62",
        unitPrice: "1250.00",
        vatRate: "21",
      },
      {
        id: "line-2",
        description: "Installatie-uren senior technicus",
        quantity: "7.5",
        unitCode: "HUR",
        unitPrice: "89.50",
        vatRate: "21",
      },
      {
        id: "line-3",
        description: "Vervangende UPS-accu (transport)",
        quantity: "2",
        unitCode: "C62",
        unitPrice: "34.95",
        vatRate: "9",
      },
    ],
    extraction: {
      sourceFileName: fileName,
      sourceByteSize: byteSize,
      engine: "mock-extractor/0.1",
      confidence: 0.72,
      extractedAt: "2026-09-08T10:00:00Z",
    },
    validated: false,
  };
}

export function emptyLine(index: number): CanonicalLine {
  return {
    id: `line-${index}-${Math.random().toString(36).slice(2, 8)}`,
    description: "",
    quantity: "1",
    unitCode: "C62",
    unitPrice: "0.00",
    vatRate: "21",
  };
}
