"use client";

import { CURRENCIES, SelectField, TextAreaField, TextField, TYPE_CODES } from "./fields";

interface Props {
  creditNote: boolean;
  status: React.ReactNode;
  /** Asks for the rate when the format needs tax in another currency (PINT-SG: SGD). */
  rate: { from: string; to: string } | null;
}

export function InvoiceDetailsCard({ creditNote, status, rate }: Props) {
  return (
    <div className="card">
      <div className="card__header">
        <h3>Factuurgegevens</h3>
        {status}
      </div>
      <div className="card__body">
        <div className="form__row">
          <TextField path="invoiceNumber" label="Factuurnummer" />
          <SelectField path="typeCode" label="Soort" options={TYPE_CODES} />
          <SelectField path="currencyCode" label="Valuta" options={CURRENCIES} />
        </div>
        <div className="form__row">
          <TextField path="issueDate" label="Factuurdatum" type="date" />
          <TextField path="dueDate" label="Vervaldatum" type="date" />
        </div>
        {rate && (
          <div className="form__row">
            <TextField
              path="taxCurrencyRate"
              label={`Wisselkoers ${rate.from} → ${rate.to}`}
              placeholder="1.3425"
              hint={`Aantal ${rate.to} voor 1 ${rate.from}, op de factuurdatum. De belasting wordt ook in ${rate.to} vermeld.`}
            />
          </div>
        )}
        <div className="form__row">
          <TextField path="buyerReference" label="Referentie afnemer" hint="Of een inkoopordernummer: één van beide is verplicht." />
          <TextField path="purchaseOrderReference" label="Inkoopordernummer" />
        </div>
        {creditNote && (
          <div className="form__row">
            <TextField path="precedingInvoiceReference" label="Oorspronkelijk factuurnummer" />
          </div>
        )}
        <TextAreaField path="note" label="Opmerking" />
      </div>
    </div>
  );
}
