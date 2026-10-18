"use client";

import { useText } from "../../../../lib/i18n";
import { paymentMeans, SelectField, TextField } from "./fields";

export function PaymentCard() {
  const t = useText();
  const text = t.review.payment;
  return (
    <div className="card">
      <div className="card__header">
        <h3>{text.title}</h3>
      </div>
      <div className="card__body">
        <div className="form__row">
          <SelectField path="payment.paymentMeansCode" label={text.means} options={paymentMeans(t)} />
          <TextField path="payment.paymentReference" label={text.reference} />
        </div>
        <div className="form__row">
          <TextField path="payment.iban" label={text.iban} />
          <TextField path="payment.bic" label={text.bic} />
          <TextField path="payment.accountName" label={text.accountName} />
        </div>
      </div>
    </div>
  );
}
