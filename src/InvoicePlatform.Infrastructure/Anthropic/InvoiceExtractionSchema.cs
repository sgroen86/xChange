using System.Text.Json;

namespace InvoicePlatform.Infrastructure.Anthropic;

/// <summary>
/// The JSON Schema the model's output is constrained to.
///
/// Three deliberate choices:
///
/// 1. Every property is listed in "required", so the model emits each field
///    explicitly rather than silently omitting what it could not find.
///
/// 2. "Not present in the document" is the empty string, not null. The obvious
///    encoding would be nullable types, but the API caps a schema at 16
///    union-typed parameters and this schema has around fifty fields:
///    "Schemas contains too many parameters with union types ... This causes
///    exponential compilation cost." Plain strings carry the same meaning here
///    because the mapper already treats empty and whitespace as absent, so a
///    blank still becomes null in the domain rather than an empty value.
///
/// 3. Monetary and quantity values are strings matching a decimal pattern, not
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
          "required": ["paymentMeansCode", "paymentMeansText", "iban", "bic", "accountName", "paymentReference"],
          "properties": {
            "paymentMeansCode": {
              "type": "string",
              "description": "UNTDID 4461 code, e.g. 30 for credit transfer."
            },
            "paymentMeansText": { "type": "string" },
            "iban": { "type": "string" },
            "bic": { "type": "string" },
            "accountName": { "type": "string" },
            "paymentReference": { "type": "string" }
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
              "lineId": { "type": "string" },
              "description": { "type": "string" },
              "itemName": { "type": "string" },
              "sellerItemIdentifier": { "type": "string" },
              "quantity": { "$ref": "#/$defs/decimalString" },
              "unitCode": {
                "type": "string",
                "description": "UN/ECE Rec 20 code, e.g. C62 for each, HUR for hour."
              },
              "unitPrice": { "$ref": "#/$defs/decimalString" },
              "baseQuantity": { "$ref": "#/$defs/decimalString" },
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
              "pageNumber": { "type": "string" },
              "sourceText": {
                "type": "string",
                "description": "The verbatim text this value was read from."
              }
            }
          }
        }
      },
      "$defs": {
        "decimalString": {
          "type": "string",
          "pattern": "^-?[0-9]+(\\.[0-9]+)?$",
          "description": "A decimal as a string, using . as the decimal separator and no thousands separators or currency symbols. Never a JSON number. Use an empty string if the value is not in the document."
        },
        "party": {
          "type": "object",
          "additionalProperties": false,
          "required": [
            "name", "legalRegistrationId", "vatIdentifier", "taxRegistrationId",
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
            "taxRegistrationId": { "type": "string" },
            "electronicAddress": {
              "type": "string",
              "description": "Electronic address for e-invoicing, e.g. a PEPPOL participant id or invoicing email."
            },
            "electronicAddressScheme": {
              "type": "string",
              "description": "Scheme of the electronic address, e.g. 0106 or EM."
            },
            "contactName": { "type": "string" },
            "contactEmail": { "type": "string" },
            "contactPhone": { "type": "string" },
            "address": {
              "type": "object",
              "additionalProperties": false,
              "required": ["streetName", "additionalStreetName", "postalZone", "cityName", "countrySubdivision", "countryCode"],
              "properties": {
                "streetName": { "type": "string" },
                "additionalStreetName": { "type": "string" },
                "postalZone": { "type": "string" },
                "cityName": { "type": "string" },
                "countrySubdivision": { "type": "string" },
                "countryCode": {
                  "type": "string",
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
              "reasonCode": { "type": "string" },
              "reason": { "type": "string" },
              "vatCategoryCode": { "type": "string" },
              "vatPercentage": { "$ref": "#/$defs/decimalString" }
            }
          }
        }
      }
    }
    """;
}
