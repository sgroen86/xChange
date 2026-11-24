"""Generates a batch of 20 fictional test invoices as PDFs, plus expected.json.

Every company, person, number and address here is made up. The seller is the
same on every invoice, as it is in xChange: an organisation converts its own
sales invoices. What varies is the buyer, the VAT situation and the layout.

    python tools/test-invoices/generate.py [output folder]

Default output: tools/test-invoices/out. Needs reportlab (pip install reportlab).
All arithmetic is Decimal; the output is deterministic (reportlab invariant mode).
"""

import json
import sys
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path

from reportlab import rl_config
from reportlab.lib.pagesizes import A4
from reportlab.pdfgen import canvas

rl_config.invariant = 1

D = Decimal
CENT = D("0.01")


def r2(value):
    return value.quantize(CENT, rounding=ROUND_HALF_UP)


SELLER = {
    "name": "Voorbeeld Techniek B.V.",
    "lines": ["Teststraat 1", "1234 AB Voorbeeldstad", "Nederland"],
    "ids": "KvK 12345678  ·  Btw NL001234567B01",
    "ids_en": "Chamber of Commerce 12345678  ·  VAT NL001234567B01",
    "bank": "IBAN NL91 ABNA 0417 1643 00  ·  BIC ABNANL2A  ·  t.n.v. Voorbeeld Techniek B.V.",
    "bank_en": "IBAN NL91 ABNA 0417 1643 00  ·  BIC ABNANL2A  ·  account Voorbeeld Techniek B.V.",
    "contact": "facturen@voorbeeld-techniek.example  ·  020-1234567",
}

NL_BUYER = {"name": "Fictieve Afnemer BV", "lines": ["Proefweg 22", "5678 CD Testdorp", "Nederland"],
            "ids": "Btw NL009876543B01  ·  KvK 87654321"}
NL_BUYER_2 = {"name": "Denkbeeldig Bouwbedrijf B.V.", "lines": ["Steigerstraat 8", "Postbus 1200", "3000 BA Rotterdam", "Nederland"],
              "ids": "Btw NL004455667B01  ·  KvK 44556677",
              "contact": "Contactpersoon: afd. Crediteuren, crediteuren@denkbeeldig-bouw.example, 010-7654321"}
BE_BUYER = {"name": "Verzonnen Handel BV", "lines": ["Kaai 15", "2000 Antwerpen", "België"],
            "ids": "BTW BE0123456749  ·  KBO 0123.456.749"}
DE_BUYER = {"name": "Erfundene Maschinen GmbH", "lines": ["Musterstraße 5", "Postfach 4711", "10115 Berlin", "Germany"],
            "ids": "VAT DE123456789", "contact": "Contact: Purchasing, einkauf@erfundene-maschinen.example"}
UK_BUYER = {"name": "Imaginary Software Ltd", "lines": ["10 Example Lane", "London EC1A 1AA", "United Kingdom"],
            "ids": "VAT GB123456789"}
US_BUYER = {"name": "Fictional Tools Inc.", "lines": ["500 Sample Avenue, Suite 12", "Springfield, IL 62701", "USA"], "ids": ""}
CONSUMER = {"name": "Dhr. P. Voorbeeld", "lines": ["Dorpsplein 3", "9999 ZZ Nergenshuizen", "Nederland"], "ids": ""}


def nl_amount(value):
    sign = "-" if value < 0 else ""
    whole, frac = f"{abs(value):.2f}".split(".")
    groups = []
    while len(whole) > 3:
        groups.insert(0, whole[-3:])
        whole = whole[:-3]
    groups.insert(0, whole)
    return f"{sign}{'.'.join(groups)},{frac}"


def en_amount(value):
    return f"{value:,.2f}"


def plain(value):
    """A quantity or price as printed: no trailing zeros beyond what is needed."""
    text = f"{value.normalize():f}"
    return text


class Invoice:
    def __init__(self, key, title, number, kind="380", currency="EUR", lang="nl", buyer=NL_BUYER,
                 issue="24-11-2026", due="24-12-2026", lines=(), doc_adjustments=(), prepaid=D("0"),
                 rounding=D("0"), notes=(), reference=None, order=None, vat_note=None, payment_ref=None,
                 date_style=None, symbol=None):
        self.key, self.title, self.number, self.kind = key, title, number, kind
        self.currency, self.lang, self.buyer = currency, lang, buyer
        self.issue, self.due = issue, due
        self.lines = list(lines)
        self.doc_adjustments = list(doc_adjustments)
        self.prepaid, self.rounding = prepaid, rounding
        self.notes, self.reference, self.order = list(notes), reference, order
        self.vat_note, self.payment_ref = vat_note, payment_ref or number
        self.date_style, self.symbol = date_style, symbol

    def amount(self, value):
        return en_amount(value) if self.lang == "en" else nl_amount(value)

    def compute(self):
        for line in self.lines:
            base = line.get("base", D("1"))
            net = r2(line["qty"] * line["price"] / base)
            for adj in line.get("adjustments", []):
                net += adj["amount"] if adj["charge"] else -adj["amount"]
            line["net"] = r2(net)
        line_total = sum((l["net"] for l in self.lines), D("0"))
        allowances = sum((a["amount"] for a in self.doc_adjustments if not a["charge"]), D("0"))
        charges = sum((a["amount"] for a in self.doc_adjustments if a["charge"]), D("0"))
        groups = {}
        for line in self.lines:
            key = (line["cat"], line["rate"])
            groups[key] = groups.get(key, D("0")) + line["net"]
        for adj in self.doc_adjustments:
            key = (adj["cat"], adj["rate"])
            groups[key] = groups.get(key, D("0")) + (adj["amount"] if adj["charge"] else -adj["amount"])
        breakdown = []
        for (cat, rate), taxable in sorted(groups.items(), key=lambda kv: (kv[0][0], -kv[0][1])):
            breakdown.append({"category": cat, "rate": rate, "taxable": r2(taxable), "tax": r2(taxable * rate / 100)})
        excl = r2(line_total - allowances + charges)
        tax = sum((b["tax"] for b in breakdown), D("0"))
        incl = excl + tax
        payable = incl - self.prepaid + self.rounding
        self.totals = {"lines": line_total, "allowances": allowances, "charges": charges, "excl": excl,
                       "tax": tax, "incl": incl, "prepaid": self.prepaid, "rounding": self.rounding, "payable": payable}
        self.breakdown = breakdown

    def expected(self):
        s = str
        return {
            "file": f"{self.key}.pdf",
            "title": self.title,
            "invoiceNumber": self.number,
            "typeCode": self.kind,
            "currency": self.currency,
            "buyer": self.buyer["name"],
            "lineCount": len(self.lines),
            "vatBreakdown": [{"category": b["category"], "rate": s(b["rate"]), "taxable": s(b["taxable"]), "tax": s(b["tax"])}
                             for b in self.breakdown],
            "totals": {"taxExclusive": s(self.totals["excl"]), "tax": s(self.totals["tax"]),
                       "taxInclusive": s(self.totals["incl"]), "prepaid": s(self.totals["prepaid"]),
                       "rounding": s(self.totals["rounding"]), "payable": s(self.totals["payable"])},
        }


T = {
    "nl": {"invoice": "FACTUUR", "credit": "CREDITNOTA", "number": "Factuurnummer", "date": "Factuurdatum",
           "due": "Vervaldatum", "to": "Factuur aan", "ref": "Uw referentie", "order": "Ordernummer",
           "cols": ["Art.nr", "Omschrijving", "Aantal", "Eenh.", "Prijs", "Per", "Btw", "Bedrag"],
           "sub": "Subtotaal", "excl": "Totaal excl. btw", "vat": "Btw", "over": "over", "incl": "Totaal incl. btw",
           "prepaid": "Reeds betaald (aanbetaling)", "rounding": "Afronding", "payable": "Te betalen",
           "pay": "Graag binnen 30 dagen overmaken onder vermelding van", "page": "Pagina"},
    "en": {"invoice": "INVOICE", "credit": "CREDIT NOTE", "number": "Invoice number", "date": "Invoice date",
           "due": "Due date", "to": "Bill to", "ref": "Your reference", "order": "Order number",
           "cols": ["Item", "Description", "Qty", "Unit", "Price", "Per", "VAT", "Amount"],
           "sub": "Subtotal", "excl": "Total excl. VAT", "vat": "VAT", "over": "on", "incl": "Total incl. VAT",
           "prepaid": "Paid in advance (deposit)", "rounding": "Rounding", "payable": "Amount due",
           "pay": "Please pay within 30 days quoting", "page": "Page"},
}


def render(inv, path):
    inv.compute()
    t = T[inv.lang]
    c = canvas.Canvas(str(path), pagesize=A4)
    width, height = A4
    page = [1]
    y = [height - 50]

    def text(x, value, size=9, bold=False, right=False):
        c.setFont("Helvetica-Bold" if bold else "Helvetica", size)
        (c.drawRightString if right else c.drawString)(x, y[0], value)

    def down(step=13):
        y[0] -= step

    def footer():
        c.setFont("Helvetica", 7)
        c.drawString(50, 30, f"{SELLER['name']}  ·  {SELLER['ids_en' if inv.lang == 'en' else 'ids']}")
        c.drawRightString(width - 50, 30, f"{t['page']} {page[0]}")

    def header_block():
        text(50, SELLER["name"], 14, bold=True); down(16)
        for l in SELLER["lines"]:
            text(50, l); down()
        text(50, SELLER["ids_en" if inv.lang == "en" else "ids"]); down()
        text(50, SELLER["contact"]); down(22)

    header_block()
    text(50, t["credit"] if inv.kind == "381" else t["invoice"], 16, bold=True); down(20)
    issue = inv.date_style[0] if inv.date_style else inv.issue
    due = inv.date_style[1] if inv.date_style else inv.due
    text(50, f"{t['number']}: {inv.number}"); text(300, f"{t['date']}: {issue}"); down()
    text(300, f"{t['due']}: {due}"); down()
    if inv.reference:
        text(50, f"{t['ref']}: {inv.reference}"); down()
    if inv.order:
        text(50, f"{t['order']}: {inv.order}"); down()
    down(6)
    text(50, t["to"] + ":", bold=True); down()
    text(50, inv.buyer["name"]); down()
    for l in inv.buyer["lines"]:
        text(50, l); down()
    if inv.buyer.get("ids"):
        text(50, inv.buyer["ids"]); down()
    if inv.buyer.get("contact"):
        text(50, inv.buyer["contact"]); down()
    down(10)

    cols = [50, 100, 300, 340, 385, 435, 470, width - 50]

    def table_head():
        for i, (x, head) in enumerate(zip(cols, t["cols"])):
            text(x, head, 8, bold=True, right=(i == len(cols) - 1))
        down(14)

    table_head()
    for line in inv.lines:
        if y[0] < 90:
            footer(); c.showPage(); page[0] += 1; y[0] = height - 60; table_head()
        values = [line.get("code", ""), line["desc"], plain(line["qty"]), line["unit"],
                  inv.amount(line["price"]), plain(line.get("base", D("1"))) if line.get("base", D("1")) != 1 else "",
                  f"{plain(line['rate'])}%", inv.amount(line["net"])]
        for i, (x, value) in enumerate(zip(cols, values)):
            text(x, value, 8, right=(i == len(values) - 1))
        down(12)
        for adj in line.get("adjustments", []):
            sign = "" if adj["charge"] else "-"
            text(100, f"  {adj['reason']}", 7); text(cols[-1], f"{sign}{inv.amount(adj['amount'])}", 7, right=True)
            down(11)

    if y[0] < 230:
        footer(); c.showPage(); page[0] += 1; y[0] = height - 60
    down(10)
    right = width - 50
    sym = inv.symbol or ("€ " if inv.currency == "EUR" else f"{inv.currency} ")

    def total(label, value, bold=False):
        text(330, label, 9, bold=bold); text(right, f"{sym}{inv.amount(value)}", 9, bold=bold, right=True); down()

    if inv.doc_adjustments:
        total(t["sub"], inv.totals["lines"])
        for adj in inv.doc_adjustments:
            total(adj["reason"], adj["amount"] if adj["charge"] else -adj["amount"])
    total(t["excl"], inv.totals["excl"])
    for b in inv.breakdown:
        total(f"{t['vat']} {plain(b['rate'])}% {t['over']} {inv.amount(b['taxable'])}", b["tax"])
    total(t["incl"], inv.totals["incl"], bold=not (inv.prepaid or inv.rounding))
    if inv.prepaid:
        total(t["prepaid"], -inv.prepaid)
    if inv.rounding:
        total(t["rounding"], inv.rounding)
    if inv.prepaid or inv.rounding:
        total(t["payable"], inv.totals["payable"], bold=True)
    down(10)
    if inv.vat_note:
        text(50, inv.vat_note, 8, bold=True); down()
    for note in inv.notes:
        text(50, note, 8); down(11)
    down(4)
    text(50, f"{t['pay']} {inv.payment_ref}.", 8); down(11)
    text(50, SELLER["bank_en" if inv.lang == "en" else "bank"], 8)
    footer()
    c.showPage()
    c.save()


def L(desc, qty, unit, price, rate=D("21"), cat="S", code="", base=None, adjustments=None):
    line = {"desc": desc, "qty": D(qty), "unit": unit, "price": D(price), "rate": D(rate), "cat": cat, "code": code}
    if base:
        line["base"] = D(base)
    if adjustments:
        line["adjustments"] = adjustments
    return line


def A(reason, amount, charge=False, rate=D("21"), cat="S"):
    return {"reason": reason, "amount": D(amount), "charge": charge, "rate": D(rate), "cat": cat}


def invoices():
    many = [L(f"Kabelgoot type {chr(65 + i % 26)}{i:02d}", str(1 + i % 7), "stuk", f"{3 + (i * 37) % 90}.{(i * 13) % 100:02d}",
              code=f"KG-{1000 + i}") for i in range(45)]
    return [
        Invoice("01-standaard-21", "Standaardfactuur, 21% btw", "VT-2026-0101",
                lines=[L("Onderhoudscontract kwartaal 4", "1", "stuk", "450.00"), L("Voorrijkosten", "1", "stuk", "35.00")]),
        Invoice("02-gemengd-9-21", "Twee btw-tarieven: 9% en 21%", "VT-2026-0102",
                lines=[L("Koffiebonen biologisch", "2.5", "kg", "18.40", rate="9", code="KF-01"),
                       L("Koffiemachine onderhoud", "1", "stuk", "89.00"),
                       L("Thee assortiment", "6", "doos", "4.95", rate="9", code="TH-06")]),
        Invoice("03-creditnota", "Creditnota (381) met negatieve bedragen", "VT-2026-C003", kind="381",
                lines=[L("Creditering retour onderhoudsset", "-2", "stuk", "64.50", code="OS-12")],
                notes=["Creditering van factuur VT-2026-0098 van 02-11-2026."]),
        Invoice("04-ic-levering-be", "Intracommunautaire levering naar België (K, 0%)", "VT-2026-0104", buyer=BE_BUYER,
                lines=[L("Pompunit PU-300", "3", "stuk", "780.00", rate="0", cat="K", code="PU-300"),
                       L("Montageset", "3", "set", "42.00", rate="0", cat="K", code="MS-3")],
                vat_note="Intracommunautaire levering, btw 0% (art. 138 Btw-richtlijn)."),
        Invoice("05-btw-verlegd-nl", "Btw verlegd binnen NL, onderaanneming (AE)", "VT-2026-0105", buyer=NL_BUYER_2,
                lines=[L("Elektrotechnische installatie project Haven", "64", "uur", "58.50", rate="0", cat="AE"),
                       L("Materiaal", "1", "partij", "1240.00", rate="0", cat="AE")],
                vat_note="Btw verlegd naar de afnemer (art. 12 lid 5 Wet OB).", order="PRJ-HAVEN-07"),
        Invoice("06-export-usd", "Export buiten de EU in USD (G, 0%), Engels", "VT-2026-0106", currency="USD", lang="en",
                buyer=US_BUYER, symbol="$ ",
                lines=[L("Control unit CU-9", "10", "pcs", "312.00", rate="0", cat="G", code="CU-9"),
                       L("Freight and insurance", "1", "pcs", "185.00", rate="0", cat="G")],
                vat_note="Export outside the EU, VAT 0%."),
        Invoice("07-diensten-uk-gbp", "Diensten aan VK-bedrijf in GBP (O), Engels", "VT-2026-0107", currency="GBP", lang="en",
                buyer=UK_BUYER, symbol="£ ",
                lines=[L("Remote support November 2026", "12", "hrs", "95.00", rate="0", cat="O")],
                vat_note="Services outside the scope of Dutch VAT; VAT reverse charged to the recipient."),
        Invoice("08-prijs-per-100", "Prijs per 100 stuks (basishoeveelheid) en artikelcodes", "VT-2026-0108",
                lines=[L("Schroeven RVS 4x50 mm", "300", "stuk", "12.50", base="100", code="SCR-450"),
                       L("Pluggen 8 mm", "250", "stuk", "6.80", base="100", code="PLG-8"),
                       L("Montage", "2", "uur", "65.00")]),
        Invoice("09-korting-factuur", "Korting van 5% op de hele factuur", "VT-2026-0109",
                lines=[L("Werkbank staal", "2", "stuk", "395.00", code="WB-2"), L("Gereedschapswand", "1", "stuk", "219.00", code="GW-1")],
                doc_adjustments=[A("Korting 5%", "50.45")]),
        Invoice("10-verzendkosten-aanbetaling", "Verzendkosten als toeslag en een aanbetaling", "VT-2026-0110",
                lines=[L("Compressor 50 liter", "1", "stuk", "649.00", code="CP-50")],
                doc_adjustments=[A("Verzendkosten", "24.95", charge=True)], prepaid=D("300.00"),
                notes=["Aanbetaling van € 300,00 ontvangen op 10-11-2026."]),
        Invoice("11-regelkorting", "Korting op één regel", "VT-2026-0111",
                lines=[L("Laptop docking station", "4", "stuk", "189.00", code="DS-4",
                         adjustments=[{"reason": "Actiekorting 10% op deze regel", "amount": D("75.60"), "charge": False}]),
                       L("USB-C kabel 2 m", "4", "stuk", "14.95", code="UC-2")]),
        Invoice("12-veel-regels", "Veel regels, meerdere pagina's (45 regels)", "VT-2026-0112", lines=many),
        Invoice("13-uren-po", "Consultancy op uurbasis met ordernummer en referentie", "VT-2026-0113",
                buyer=NL_BUYER_2, reference="Afd. ICT / J. de Vries", order="PO-4502",
                lines=[L("Advies netwerkinrichting week 46", "16", "uur", "112.50"),
                       L("Advies netwerkinrichting week 47", "12.5", "uur", "112.50"),
                       L("Reiskosten", "180", "km", "0.23")]),
        Invoice("14-particulier-afronding", "Particulier zonder btw-nummer, contante afronding", "VT-2026-0114", buyer=CONSUMER,
                lines=[L("Reparatie wasmachine", "1", "stuk", "68.33")], rounding=D("0.02"),
                notes=["Contant betaald, afgerond op 5 cent."]),
        Invoice("15-vrijgesteld", "Vrijgestelde dienst (E) met reden", "VT-2026-0115",
                lines=[L("Training veilig werken (erkend beroepsonderwijs)", "8", "deeln.", "245.00", rate="0", cat="E")],
                vat_note="Vrijgesteld van btw op grond van art. 11 lid 1 sub o Wet OB."),
        Invoice("16-diensten-be-verlegd", "Diensten aan Belgisch bedrijf, btw verlegd (AE)", "VT-2026-0116", buyer=BE_BUYER,
                lines=[L("Inspectie installaties Antwerpen", "1", "stuk", "1850.00", rate="0", cat="AE")],
                vat_note="Btw verlegd, art. 196 Btw-richtlijn.", payment_ref="+++123/4567/89012+++"),
        Invoice("17-de-engels-postfach", "Duitse afnemer met Postfach en contact, Engels, IC-levering", "VT-2026-0117", lang="en",
                buyer=DE_BUYER, order="BE-2026-0815",
                lines=[L("Sensor module SM-2", "40", "pcs", "27.80", rate="0", cat="K", code="SM-2"),
                       L("Calibration certificate", "40", "pcs", "4.50", rate="0", cat="K")],
                vat_note="Intra-community supply, VAT 0% (Art. 138 VAT Directive)."),
        Invoice("18-groot-bedrag-notatie", "Grote bedragen, duizendtallen en uitgeschreven datum", "VT-2026-0118",
                date_style=("24 november 2026", "24 december 2026"),
                lines=[L("Zonnepaneleninstallatie bedrijfspand", "1", "stuk", "48750.00"),
                       L("Omvormers 25 kW", "3", "stuk", "3125.50", code="INV-25")]),
        Invoice("19-correctieregel", "Negatieve correctieregel in een positieve factuur", "VT-2026-0119",
                lines=[L("Maandabonnement monitoring december", "1", "stuk", "149.00"),
                       L("Correctie te veel gefactureerd november", "-1", "stuk", "49.00")],
                notes=["Correctie betreft factuur VT-2026-0090."]),
        Invoice("20-aanbetaling-kg", "Levensmiddelen 9% in kg met decimalen en aanbetaling", "VT-2026-0120",
                lines=[L("Kaas belegen", "3.75", "kg", "14.90", rate="9", code="KS-B"),
                       L("Olijfolie extra vierge 5 l", "2", "blik", "38.50", rate="9", code="OO-5"),
                       L("Bezorging", "1", "stuk", "12.50")],
                prepaid=D("50.00")),
    ]


def main():
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).parent / "out"
    out.mkdir(parents=True, exist_ok=True)
    expected = []
    for inv in invoices():
        render(inv, out / f"{inv.key}.pdf")
        expected.append(inv.expected())
    (out / "expected.json").write_text(json.dumps(expected, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"{len(expected)} invoices written to {out}")


if __name__ == "__main__":
    main()
