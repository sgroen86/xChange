"use client";

import { CURRENCIES, SelectField, TextAreaField, TextField, TYPE_CODES } from "./fields";

export function InvoiceDetailsCard({ creditNote, status }: { creditNote: boolean; status: React.ReactNode }) {
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
