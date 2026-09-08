using System.Text.Json;

namespace InvoicePlatform.Infrastructure.Anthropic;

/// <summary>
/// The JSON Schema the model's output is constrained to.
///
/// Two deliberate choices:
///
/// 1. Every property is listed in "required" and every leaf accepts null. The
///    model must therefore emit each field explicitly, and "not present in the
///    document" is expressed as null rather than as a guessed value or a
///    missing key.
///
/// 2. Monetary and quantity values are strings matching a decimal pattern, not
///    JSON numbers. A JSON number would invite a float representation, and
///    1234.10 must not become 1234.0999999. They are parsed into
///    <see cref="decimal"/> on arrival (CLAUDE.md, hard rule 3).
/// </summary>
internal static class InvoiceExtractionSchema
{
    public static IReadOnlyDictionary<string, JsonElement> Build()
    {
        using var document = JsonDocument.Parse(SchemaJson);
        return document.RootElement
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone());
    }

    private const string SchemaJson = """
    {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "typeCode", "invoiceNumber", "issueDate", "dueDate", "currencyCode", "note",
        "seller", "buyer", "purchaseOrderReference", "buyerReference", "payment",
        "lines", "allowancesAndCharges", "vatBreakdown", "totals", "evidence"
      ],
      "properties": {
        "typeCode": {
          "type": ["string", "null"],
          "description": "UNTDID 1001 document type code. 380 = commercial invoice, 381 = credit note."
        },
        "invoiceNumber": { "type": ["string", "null"] },
        "issueDate": {
          "type": ["string", "null"],
          "description": "Issue date as yyyy-MM-dd."
        },
        "dueDate": {
          "type": ["string", "null"],
          "description": "Payment due date as yyyy-MM-dd."
        },
        "currencyCode": {
          "type": ["string", "null"],
          "description": "ISO 4217 code, e.g. EUR."
        },
        "note": { "type": ["string", "null"] },
        "seller": { "$ref": "#/$defs/party" },
        "buyer": { "$ref": "#/$defs/party" },
        "purchaseOrderReference": { "type": ["string", "null"] },
        "buyerReference": { "type": ["string", "null"] },
        "payment": {
          "type": ["object", "null"],
          "additionalProperties": false,
          "required": ["paymentMeansCode", "paymentMeansText", "iban", "bic", "accountName", "paymentReference"],
          "properties": {
            "paymentMeansCode": {
              "type": ["string", "null"],
              "description": "UNTDID 4461 code, e.g. 30 for credit transfer."
            },
            "paymentMeansText": { "type": ["string", "null"] },
            "iban": { "type": ["string", "null"] },
            "bic": { "type": ["string", "null"] },
            "accountName": { "type": ["string", "null"] },
            "paymentReference": { "type": ["string", "null"] }
          }
        },
        "lines": {
          "type": "array",
          "items": {
            "type": "object",
            "additionalProperties": false,
            "required": [
              "lineId", "description", "itemName", "sellerItemIdentifier", "quantity",
              "unitCode", "unitPrice", "baseQuantity", "netAmount", "vatCategoryCode",
              "vatPercentage", "allowancesAndCharges"
            ],
            "properties": {
              "lineId": { "type": ["string", "null"] },
              "description": { "type": ["string", "null"] },
              "itemName": { "type": ["string", "null"] },
              "sellerItemIdentifier": { "type": ["string", "null"] },
              "quantity": { "$ref": "#/$defs/decimalString" },
              "unitCode": {
                "type": ["string", "null"],
                "description": "UN/ECE Rec 20 code, e.g. C62 for each, HUR for hour."
              },
              "unitPrice": { "$ref": "#/$defs/decimalString" },
              "baseQuantity": { "$ref": "#/$defs/decimalString" },
              "netAmount": {
                "$ref": "#/$defs/decimalString",
                "description": "Line net amount excluding VAT, exactly as printed."
              },
              "vatCategoryCode": {
                "type": ["string", "null"],
                "description": "UNTDID 5305 code: S standard, Z zero rated, E exempt, AE reverse charge, G export, K intra-community, O out of scope."
              },
              "vatPercentage": { "$ref": "#/$defs/decimalString" },
              "allowancesAndCharges": { "$ref": "#/$defs/adjustments" }
            }
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
              "vatCategoryCode": { "type": ["string", "null"] },
              "vatPercentage": { "$ref": "#/$defs/decimalString" },
              "taxableAmount": { "$ref": "#/$defs/decimalString" },
              "taxAmount": { "$ref": "#/$defs/decimalString" },
              "exemptionReason": { "type": ["string", "null"] }
            }
          }
        },
        "totals": {
          "type": ["object", "null"],
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
        "evidence": {
          "type": "array",
          "description": "Provenance for the fields you filled in. Omit entries for fields you set to null.",
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
              "pageNumber": { "type": ["integer", "null"] },
              "sourceText": {
                "type": ["string", "null"],
                "description": "The verbatim text this value was read from."
              }
            }
          }
        }
      },
      "$defs": {
        "decimalString": {
          "type": ["string", "null"],
          "pattern": "^-?[0-9]+(\\.[0-9]+)?$",
          "description": "A decimal as a string, using . as the decimal separator and no thousands separators or currency symbols. Never a JSON number."
        },
        "party": {
          "type": ["object", "null"],
          "additionalProperties": false,
          "required": [
            "name", "legalRegistrationId", "vatIdentifier", "taxRegistrationId",
            "electronicAddress", "electronicAddressScheme",
            "contactName", "contactEmail", "contactPhone", "address"
          ],
          "properties": {
            "name": { "type": ["string", "null"] },
            "legalRegistrationId": {
              "type": ["string", "null"],
              "description": "Company registration number, e.g. KvK or trade register id."
            },
            "vatIdentifier": {
              "type": ["string", "null"],
              "description": "VAT number, e.g. NL123456789B01."
            },
            "taxRegistrationId": { "type": ["string", "null"] },
            "electronicAddress": {
              "type": ["string", "null"],
              "description": "Electronic address for e-invoicing, e.g. a PEPPOL participant id or invoicing email."
            },
            "electronicAddressScheme": {
              "type": ["string", "null"],
              "description": "Scheme of the electronic address, e.g. 0106 or EM."
            },
            "contactName": { "type": ["string", "null"] },
            "contactEmail": { "type": ["string", "null"] },
            "contactPhone": { "type": ["string", "null"] },
            "address": {
              "type": ["object", "null"],
              "additionalProperties": false,
              "required": ["streetName", "additionalStreetName", "postalZone", "cityName", "countrySubdivision", "countryCode"],
              "properties": {
                "streetName": { "type": ["string", "null"] },
                "additionalStreetName": { "type": ["string", "null"] },
                "postalZone": { "type": ["string", "null"] },
                "cityName": { "type": ["string", "null"] },
                "countrySubdivision": { "type": ["string", "null"] },
                "countryCode": {
                  "type": ["string", "null"],
                  "description": "ISO 3166-1 alpha-2, e.g. NL."
                }
              }
            }
          }
        },
        "adjustments": {
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
              "reasonCode": { "type": ["string", "null"] },
              "reason": { "type": ["string", "null"] },
              "vatCategoryCode": { "type": ["string", "null"] },
              "vatPercentage": { "$ref": "#/$defs/decimalString" }
            }
          }
        }
      }
    }
    """;
}
