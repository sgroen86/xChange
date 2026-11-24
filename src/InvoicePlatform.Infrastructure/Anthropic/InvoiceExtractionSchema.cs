using System.Text.Json;

namespace InvoicePlatform.Infrastructure.Anthropic;

/// <summary>
/// The JSON Schemas the model's output is constrained to: one for the header,
/// one for the lines. <see cref="AnthropicInvoiceInterpreter"/> sends both
/// with the same PDF at the same time and merges the answers.
///
/// Why two: the API rejects a schema whose compiled grammar is too large ("The
/// compiled grammar is too large"). The single schema before the split had 82
/// fields counting every $ref written out, was accepted, and left no room:
/// contact details, the extra address line, BIC, account name, article code
/// and base quantity had to stay out. Split, the header has 74 fields and the
/// lines 25 (InvoiceExtractionSchemaTests guards both against growing past 82),
/// and each answer is shorter, which keeps a large invoice within the token
/// and time limits.
///
/// Only fields the review screen shows are asked for: a value read by the model
/// must be one a reviewer can see and correct before it reaches an e-invoice.
/// The item name (BT-153) is not asked for: UblInvoiceWriter writes it from the
/// line description, a settled decision. The domain keeps the fields this
/// schema does not ask for (country subdivision, a second tax registration,
/// payment means text, item name); they simply stay null.
///
/// Three deliberate choices, unchanged from the single schema:
///
/// 1. Every property is listed in "required", so the model emits each field
///    explicitly rather than silently omitting what it could not find.
///
/// 2. "Not present in the document" is the empty string, not null. The API
///    caps a schema at 16 union-typed parameters, and these schemas have far
///    more optional values than that. The mapper treats empty and whitespace
///    as absent, so a blank still becomes null in the domain.
///
/// 3. Monetary and quantity values are strings, not JSON numbers, so 1234.10
///    never becomes 1234.0999999; they are parsed into decimal on arrival
///    (CLAUDE.md, hard rule 3). They carry no regex "pattern": that blows up
///    the grammar and would contradict the empty-string convention. ParseDecimal
///    is the real guard.
///
/// Changing a schema invalidates the prompt cache, so the two calls cannot share
/// a cached PDF; each pays for it in full.
/// </summary>
internal static class InvoiceExtractionSchema
{
    public static IReadOnlyDictionary<string, JsonElement> Header() => Parse(HeaderJson);

    public static IReadOnlyDictionary<string, JsonElement> Lines() => Parse(LinesJson);

    private static IReadOnlyDictionary<string, JsonElement> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone());
    }

    private const string Evidence = """
        {
          "type": "array",
          "description": "Provenance for the fields you filled in. Omit entries for fields you left empty.",
          "items": {
            "type": "object",
            "additionalProperties": false,
            "required": ["field", "confidence", "pageNumber", "sourceText"],
            "properties": {
              "field": {
                "type": "string",
                "description": "Dotted path of the field, e.g. totals.payableAmount or lines[0].netAmount."
              },
              "confidence": {
                "$ref": "#/$defs/decimalString",
                "description": "Your confidence from 0 to 1, as a decimal string."
              },
              "pageNumber": { "type": "string" },
              "sourceText": {
                "type": "string",
                "description": "The verbatim text this value was read from."
              }
            }
          }
        }
        """;

    private const string DecimalString = """
        {
          "type": "string",
          "description": "A decimal as a string, e.g. 1234.50, using . as the decimal separator and no thousands separators or currency symbols. Never a JSON number. Empty string if the value is not in the document."
        }
        """;

    private const string Adjustments = """
        {
          "type": "array",
          "items": {
            "type": "object",
            "additionalProperties": false,
            "required": ["isCharge", "amount", "baseAmount", "percentage", "reasonCode", "reason", "vatCategoryCode", "vatPercentage"],
            "properties": {
              "isCharge": {
                "type": "boolean",
                "description": "true for a charge that increases the total, false for an allowance or discount."
              },
              "amount": { "$ref": "#/$defs/decimalString" },
              "baseAmount": { "$ref": "#/$defs/decimalString" },
              "percentage": { "$ref": "#/$defs/decimalString" },
              "reasonCode": { "type": "string" },
              "reason": { "type": "string" },
              "vatCategoryCode": { "type": "string" },
              "vatPercentage": { "$ref": "#/$defs/decimalString" }
            }
          }
        }
        """;

    private const string HeaderJson = $$"""
    {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "typeCode", "invoiceNumber", "issueDate", "dueDate", "currencyCode", "note",
        "seller", "buyer", "purchaseOrderReference", "buyerReference", "payment",
        "allowancesAndCharges", "vatBreakdown", "totals", "evidence"
      ],
      "properties": {
        "typeCode": {
          "type": "string",
          "description": "UNTDID 1001 document type code. 380 = commercial invoice, 381 = credit note."
        },
        "invoiceNumber": { "type": "string" },
        "issueDate": {
          "type": "string",
          "description": "Issue date as yyyy-MM-dd."
        },
        "dueDate": {
          "type": "string",
          "description": "Payment due date as yyyy-MM-dd."
        },
        "currencyCode": {
          "type": "string",
          "description": "ISO 4217 code, e.g. EUR."
        },
        "note": { "type": "string" },
        "seller": { "$ref": "#/$defs/party" },
        "buyer": { "$ref": "#/$defs/party" },
        "purchaseOrderReference": { "type": "string" },
        "buyerReference": { "type": "string" },
        "payment": {
          "type": "object",
          "additionalProperties": false,
          "required": ["paymentMeansCode", "iban", "bic", "accountName", "paymentReference"],
          "properties": {
            "paymentMeansCode": {
              "type": "string",
              "description": "UNTDID 4461 code, e.g. 30 for credit transfer."
            },
            "iban": { "type": "string" },
            "bic": {
              "type": "string",
              "description": "BIC / SWIFT code of the payee's bank, as printed."
            },
            "accountName": {
              "type": "string",
              "description": "Name of the payee's account holder, as printed."
            },
            "paymentReference": { "type": "string" }
          }
        },
        "allowancesAndCharges": { "$ref": "#/$defs/adjustments" },
        "vatBreakdown": {
          "type": "array",
          "items": {
            "type": "object",
            "additionalProperties": false,
            "required": ["vatCategoryCode", "vatPercentage", "taxableAmount", "taxAmount", "exemptionReason"],
            "properties": {
              "vatCategoryCode": { "type": "string" },
              "vatPercentage": { "$ref": "#/$defs/decimalString" },
              "taxableAmount": { "$ref": "#/$defs/decimalString" },
              "taxAmount": { "$ref": "#/$defs/decimalString" },
              "exemptionReason": { "type": "string" }
            }
          }
        },
        "totals": {
          "type": "object",
          "additionalProperties": false,
          "required": [
            "lineExtensionAmount", "allowanceTotalAmount", "chargeTotalAmount",
            "taxExclusiveAmount", "taxAmount", "taxInclusiveAmount",
            "prepaidAmount", "roundingAmount", "payableAmount"
          ],
          "properties": {
            "lineExtensionAmount": {
              "$ref": "#/$defs/decimalString",
              "description": "Sum of line net amounts."
            },
            "allowanceTotalAmount": { "$ref": "#/$defs/decimalString" },
            "chargeTotalAmount": { "$ref": "#/$defs/decimalString" },
            "taxExclusiveAmount": {
              "$ref": "#/$defs/decimalString",
              "description": "Net total: the total excluding VAT."
            },
            "taxAmount": {
              "$ref": "#/$defs/decimalString",
              "description": "VAT total."
            },
            "taxInclusiveAmount": {
              "$ref": "#/$defs/decimalString",
              "description": "Gross total: the total including VAT."
            },
            "prepaidAmount": { "$ref": "#/$defs/decimalString" },
            "roundingAmount": { "$ref": "#/$defs/decimalString" },
            "payableAmount": {
              "$ref": "#/$defs/decimalString",
              "description": "Amount actually due for payment."
            }
          }
        },
        "evidence": {{Evidence}}
      },
      "$defs": {
        "decimalString": {{DecimalString}},
        "party": {
          "type": "object",
          "additionalProperties": false,
          "required": [
            "name", "legalRegistrationId", "vatIdentifier",
            "electronicAddress", "electronicAddressScheme",
            "contactName", "contactEmail", "contactPhone", "address"
          ],
          "properties": {
            "name": { "type": "string" },
            "legalRegistrationId": {
              "type": "string",
              "description": "Company registration number, e.g. KvK or trade register id."
            },
            "vatIdentifier": {
              "type": "string",
              "description": "VAT number, e.g. NL123456789B01."
            },
            "electronicAddress": {
              "type": "string",
              "description": "Electronic address for e-invoicing, e.g. a PEPPOL participant id or invoicing email."
            },
            "electronicAddressScheme": {
              "type": "string",
              "description": "Scheme of the electronic address, e.g. 0106 or EM."
            },
            "contactName": {
              "type": "string",
              "description": "Name of the contact person or department, as printed."
            },
            "contactEmail": {
              "type": "string",
              "description": "Email address of the contact, as printed."
            },
            "contactPhone": {
              "type": "string",
              "description": "Telephone number of the contact, as printed."
            },
            "address": {
              "type": "object",
              "additionalProperties": false,
              "required": ["streetName", "additionalStreetName", "postalZone", "cityName", "countryCode"],
              "properties": {
                "streetName": { "type": "string" },
                "additionalStreetName": {
                  "type": "string",
                  "description": "A second address line, e.g. a building, unit or PO box, as printed."
                },
                "postalZone": { "type": "string" },
                "cityName": { "type": "string" },
                "countryCode": {
                  "type": "string",
                  "description": "ISO 3166-1 alpha-2, e.g. NL."
                }
              }
            }
          }
        },
        "adjustments": {{Adjustments}}
      }
    }
    """;

    private const string LinesJson = $$"""
    {
      "type": "object",
      "additionalProperties": false,
      "required": ["lines", "evidence"],
      "properties": {
        "lines": {
          "type": "array",
          "items": {
            "type": "object",
            "additionalProperties": false,
            "required": [
              "lineId", "description", "sellerItemIdentifier", "quantity", "unitCode", "unitPrice",
              "baseQuantity", "netAmount", "vatCategoryCode", "vatPercentage", "allowancesAndCharges"
            ],
            "properties": {
              "lineId": { "type": "string" },
              "description": { "type": "string" },
              "sellerItemIdentifier": {
                "type": "string",
                "description": "The seller's article or item code for this line, as printed."
              },
              "quantity": { "$ref": "#/$defs/decimalString" },
              "unitCode": {
                "type": "string",
                "description": "UN/ECE Rec 20 code, e.g. C62 for each, HUR for hour."
              },
              "unitPrice": { "$ref": "#/$defs/decimalString" },
              "baseQuantity": {
                "$ref": "#/$defs/decimalString",
                "description": "The quantity the unit price refers to when the price is per more than one unit, e.g. 100 for a price per 100 pieces. Empty string when the price is per single unit."
              },
              "netAmount": {
                "$ref": "#/$defs/decimalString",
                "description": "Line net amount excluding VAT, exactly as printed."
              },
              "vatCategoryCode": {
                "type": "string",
                "description": "UNTDID 5305 code: S standard, Z zero rated, E exempt, AE reverse charge, G export, K intra-community, O out of scope."
              },
              "vatPercentage": { "$ref": "#/$defs/decimalString" },
              "allowancesAndCharges": { "$ref": "#/$defs/adjustments" }
            }
          }
        },
        "evidence": {{Evidence}}
      },
      "$defs": {
        "decimalString": {{DecimalString}},
        "adjustments": {{Adjustments}}
      }
    }
    """;
}
