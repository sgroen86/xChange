"use client";

import { PAYMENT_MEANS, SelectField, TextField } from "./fields";

export function PaymentCard() {
  return (
    <div className="card">
      <div className="card__header">
        <h3>Betaling</h3>
      </div>
      <div className="card__body">
        <div className="form__row">
          <SelectField path="payment.paymentMeansCode" label="Betaalwijze" options={PAYMENT_MEANS} />
          <TextField path="payment.paymentReference" label="Betalingskenmerk" />
        </div>
        <div className="form__row">
          <TextField path="payment.iban" label="IBAN" />
          <TextField path="payment.bic" label="BIC" />
          <TextField path="payment.accountName" label="Ten name van" />
        </div>
      </div>
    </div>
  );
}
