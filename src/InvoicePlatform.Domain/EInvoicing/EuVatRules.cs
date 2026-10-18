using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>
/// The EN 16931 VAT category rules (BR-S, BR-Z, BR-E, BR-AE, BR-G, BR-O, and
/// categories the model cannot support), for rulesets built on European VAT.
/// Rate and identifier rules per line and per document allowance or charge;
/// exemption reasons on the calculated breakdown.
/// </summary>
internal static class EuVatRules
{
    public static IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice)
    {
        var findings = new Findings();
        VatCategories(invoice, findings);
        return findings.Violations;
    }

    private static void VatCategories(CanonicalInvoiceDraft invoice, Findings findings)
    {
        // Rates, per line and per document allowance or charge.
        for (var index = 0; index < invoice.Lines.Count; index++)
        {
            var line = invoice.Lines[index];
            Rate(line.VatCategoryCode, line.VatPercentage, CategorySource.Line,
                $"lines[{index}]", $"Regel {Findings.Number(index)}", findings);
        }

        for (var index = 0; index < invoice.AllowancesAndCharges.Count; index++)
        {
            var adjustment = invoice.AllowancesAndCharges[index];
            Rate(adjustment.VatCategoryCode, adjustment.VatPercentage,
                adjustment.IsCharge ? CategorySource.Charge : CategorySource.Allowance,
                $"allowancesAndCharges[{index}]",
                $"{(adjustment.IsCharge ? "Toeslag" : "Korting")} {Findings.Number(index)} op factuurniveau", findings);
        }

        // Identifiers each category requires (or, for O, forbids).
        var seller = invoice.Seller;
        var buyer = invoice.Buyer;
        var sellerHasVatOrTaxId = !Findings.Missing(seller?.VatIdentifier) || !Findings.Missing(seller?.TaxRegistrationId);
        var used = InvoiceFacts.CategoriesInUse(invoice).ToList();

        foreach (var (category, source) in used)
        {
            var suffix = source switch
            {
                CategorySource.Line => "02",
                CategorySource.Allowance => "03",
                _ => "04",
            };

            switch (category)
            {
                case "S" or "Z" or "E" when !sellerHasVatOrTaxId:
                    findings.Add($"BR-{category}-{suffix}", "BT-31", "seller.vatIdentifier",
                        $"Btw-nummer van de verkoper is verplicht bij btw-categorie {category}.");
                    break;

                case "AE":
                    if (!sellerHasVatOrTaxId)
                    {
                        findings.Add($"BR-AE-{suffix}", "BT-31", "seller.vatIdentifier",
                            "Btw-nummer van de verkoper is verplicht bij verlegde btw (AE).");
                    }

                    if (Findings.Missing(buyer?.VatIdentifier) && Findings.Missing(buyer?.LegalRegistrationId))
                    {
                        findings.Add($"BR-AE-{suffix}", "BT-48", "buyer.vatIdentifier",
                            "Btw-nummer of KvK-nummer van de afnemer is verplicht bij verlegde btw (AE).");
                    }

                    break;

                case "G" when Findings.Missing(seller?.VatIdentifier):
                    findings.Add($"BR-G-{suffix}", "BT-31", "seller.vatIdentifier",
                        "Btw-nummer van de verkoper is verplicht bij export buiten de EU (G).");
                    break;

                case "O":
                    if (!Findings.Missing(seller?.VatIdentifier))
                    {
                        findings.Add($"BR-O-{suffix}", "BT-31", "seller.vatIdentifier",
                            "Bij 'niet onderworpen aan btw' (O) mag het btw-nummer van de verkoper niet op de factuur staan.");
                    }

                    if (!Findings.Missing(buyer?.VatIdentifier))
                    {
                        findings.Add($"BR-O-{suffix}", "BT-48", "buyer.vatIdentifier",
                            "Bij 'niet onderworpen aan btw' (O) mag het btw-nummer van de afnemer niet op de factuur staan.");
                    }

                    break;
            }
        }

        if (used.Any(u => u.Category == "O") && used.Any(u => u.Category != "O"))
        {
            findings.Add("BR-O-12", "BT-151", "lines",
                "Btw-categorie O (niet onderworpen aan btw) kan niet samen met andere btw-categorieën op één factuur.");
        }

        // Exemption reasons in the calculated breakdown.
        for (var index = 0; index < invoice.VatBreakdown.Count; index++)
        {
            var bucket = invoice.VatBreakdown[index];
            var path = $"vatBreakdown[{index}].exemptionReason";
            var hasReason = !Findings.Missing(bucket.ExemptionReason);

            switch (bucket.VatCategoryCode)
            {
                case "S" or "Z" when hasReason:
                    findings.Add($"BR-{bucket.VatCategoryCode}-10", "BT-120", path,
                        $"Bij btw-categorie {bucket.VatCategoryCode} hoort geen vrijstellingsreden.");
                    break;

                case "E" or "AE" or "G" or "O" when !hasReason:
                    findings.Add($"BR-{bucket.VatCategoryCode}-10", "BT-120", path,
                        $"Vrijstellingsreden ontbreekt voor btw-categorie {bucket.VatCategoryCode}.");
                    break;
            }
        }
    }

    /// <summary>
    /// The rate each category requires. O has no rate at all; the writer leaves
    /// it out, so it is not something the reviewer needs to fix.
    /// </summary>
    private static void Rate(
        string? category, decimal? rate, CategorySource source, string path, string label, Findings findings)
    {
        var suffix = source switch
        {
            CategorySource.Line => "05",
            CategorySource.Allowance => "06",
            _ => "07",
        };

        switch (category)
        {
            case "S" when rate is not > 0m:
                findings.Add($"BR-S-{suffix}", "BT-152", $"{path}.vatPercentage",
                    $"{label}: btw-categorie S (standaardtarief) vraagt een btw-percentage boven nul.");
                break;

            case "Z" or "E" or "AE" or "G" when rate is { } actual && actual != 0m:
                findings.Add($"BR-{category}-{suffix}", "BT-152", $"{path}.vatPercentage",
                    $"{label}: btw-categorie {category} vraagt btw-percentage 0.");
                break;

            case "K":
                findings.Add("XC-VAT-UNSUPPORTED", "BT-151", $"{path}.vatCategoryCode",
                    $"{label}: btw-categorie K (intracommunautaire levering) vraagt een leverdatum en leveringsland, "
                    + "die xChange nog niet vastlegt.");
                break;

            case "L" or "M" or "B":
                findings.Add("XC-VAT-UNSUPPORTED", "BT-151", $"{path}.vatCategoryCode",
                    $"{label}: btw-categorie {category} wordt niet ondersteund.");
                break;
        }
    }
}
