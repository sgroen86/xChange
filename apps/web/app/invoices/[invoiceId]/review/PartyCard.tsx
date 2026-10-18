"use client";

import { useText } from "../../../../lib/i18n";
import { endpointSchemes, registrationSchemes, SelectField, TextField } from "./fields";

/** Seller or buyer. The organisation itself is the seller: these are its own sales invoices. */
export function PartyCard({ side, title, children }: { side: "seller" | "buyer"; title: string; children?: React.ReactNode }) {
  const t = useText();
  const text = t.review.party;
  return (
    <div className="card">
      <div className="card__header">
        <h3>{title}</h3>
      </div>
      <div className="card__body">
        {children}
        <div className="form__row">
          <TextField path={`${side}.name`} label={text.name} />
          <TextField path={`${side}.vatIdentifier`} label={text.vatIdentifier} placeholder="NL123456789B01" />
        </div>
        <div className="form__row">
          <TextField path={`${side}.legalRegistrationId`} label={text.registrationId} />
          <SelectField path={`${side}.legalRegistrationScheme`} label={text.registrationScheme} options={registrationSchemes(t)} />
        </div>
        <div className="form__row">
          <TextField path={`${side}.electronicAddress`} label={text.electronicAddress} />
          <SelectField path={`${side}.electronicAddressScheme`} label={text.electronicAddressScheme} options={endpointSchemes(t)} />
        </div>
        <div className="form__row">
          <TextField path={`${side}.address.streetName`} label={text.street} />
          <TextField path={`${side}.address.postalZone`} label={text.postalCode} />
        </div>
        <div className="form__row">
          <TextField path={`${side}.address.cityName`} label={text.city} />
          <TextField path={`${side}.address.countryCode`} label={text.countryCode} placeholder="NL" />
        </div>
      </div>
    </div>
  );
}
