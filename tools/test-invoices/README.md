# Test invoices

Twenty fictional invoice PDFs for trying out reading and booking. Use them as a batch upload in the
app, or with `tools/model-comparison`. Every company, person, number and address is made up, so
the PDFs can live in the repository, unlike real customer PDFs.

```
pip install reportlab
python tools/test-invoices/generate.py            # writes tools/test-invoices/out
```

Every invoice has the same seller, *Voorbeeld Techniek B.V.*, as it would in xChange, where an
organisation converts its own sales invoices. What varies is the buyer, the VAT situation and the
layout. `out/expected.json` holds the correct invoice number, type, currency, number of lines, VAT
breakdown and totals of each invoice. The generator computes them with `Decimal`, so a reading can
be checked against them.

| File | What it tests |
|---|---|
| 01-standaard-21 | Plain invoice, 21% VAT |
| 02-gemengd-9-21 | Two VAT rates, a quantity in kg with decimals |
| 03-creditnota | Credit note (381), negative amounts, reference to the original invoice |
| 04-ic-levering-be | Intra-community supply to Belgium (K, 0%) |
| 05-btw-verlegd-nl | Domestic reverse charge, subcontracting (AE); buyer with a PO box and a contact |
| 06-export-usd | Export outside the EU in USD (G, 0%), English |
| 07-diensten-uk-gbp | Services to a UK company in GBP (O), English |
| 08-prijs-per-100 | Price per 100 pieces (base quantity) and article codes |
| 09-korting-factuur | 5% discount on the whole invoice |
| 10-verzendkosten-aanbetaling | Shipping as an invoice-level charge, and a deposit already paid |
| 11-regelkorting | Discount on a single line |
| 12-veel-regels | 45 lines over several pages |
| 13-uren-po | Hourly consultancy with an order number and a reference |
| 14-particulier-afronding | Consumer without a VAT number, cash rounding |
| 15-vrijgesteld | VAT-exempt service (E) with the exemption reason |
| 16-diensten-be-verlegd | Services to a Belgian company, reverse charge (AE), Belgian structured payment reference |
| 17-de-engels-postfach | German buyer with a Postfach and a contact, English, intra-community |
| 18-groot-bedrag-notatie | Large amounts with thousands separators, dates written out |
| 19-correctieregel | A negative correction line in a positive invoice |
| 20-aanbetaling-kg | Food at 9% in kg with decimals, a deposit already paid |

All of these are text PDFs. A scanned invoice (an image of paper) is not among them.
