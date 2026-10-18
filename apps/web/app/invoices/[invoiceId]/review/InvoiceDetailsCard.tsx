"use client";

import { useText } from "../../../../lib/i18n";
import { CURRENCIES, SelectField, TextAreaField, TextField, typeCodes } from "./fields";

interface Props {
  creditNote: boolean;
  status: React.ReactNode;
  /** Asks for the rate when the format needs tax in another currency (PINT-SG: SGD). */
  rate: { from: string; to: string } | null;
}

export function InvoiceDetailsCard({ creditNote, status, rate }: Props) {
  const t = useText();
  const text = t.review.details;
  return (
    <div className="card">
      <div className="card__header">
        <h3>{text.title}</h3>
        {status}
      </div>
      <div className="card__body">
        <div className="form__row">
          <TextField path="invoiceNumber" label={text.invoiceNumber} />
          <SelectField path="typeCode" label={text.type} options={typeCodes(t)} />
          <SelectField path="currencyCode" label={text.currency} options={CURRENCIES} />
        </div>
        <div className="form__row">
          <TextField path="issueDate" label={text.issueDate} type="date" />
          <TextField path="dueDate" label={text.dueDate} type="date" />
        </div>
        {rate && (
          <div className="form__row">
            <TextField
              path="taxCurrencyRate"
              label={text.rate(rate.from, rate.to)}
              placeholder="1.3425"
              hint={text.rateHint(rate.from, rate.to)}
            />
          </div>
        )}
        <div className="form__row">
          <TextField path="buyerReference" label={text.buyerReference} hint={text.buyerReferenceHint} />
          <TextField path="purchaseOrderReference" label={text.purchaseOrder} />
        </div>
        {creditNote && (
          <div className="form__row">
            <TextField path="precedingInvoiceReference" label={text.precedingInvoice} />
          </div>
        )}
        <TextAreaField path="note" label={text.note} />
      </div>
    </div>
  );
}
