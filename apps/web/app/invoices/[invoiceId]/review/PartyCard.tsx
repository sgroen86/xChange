"use client";

import { ENDPOINT_SCHEMES, REGISTRATION_SCHEMES, SelectField, TextField } from "./fields";

/** Seller or buyer. The organisation itself is the seller: these are its own sales invoices. */
export function PartyCard({ side, title }: { side: "seller" | "buyer"; title: string }) {
  return (
    <div className="card">
      <div className="card__header">
        <h3>{title}</h3>
      </div>
      <div className="card__body">
        <div className="form__row">
          <TextField path={`${side}.name`} label="Naam" />
          <TextField path={`${side}.vatIdentifier`} label="Btw-nummer" placeholder="NL123456789B01" />
        </div>
        <div className="form__row">
          <TextField path={`${side}.legalRegistrationId`} label="KvK-nummer" />
          <SelectField path={`${side}.legalRegistrationScheme`} label="Schema registratienummer" options={REGISTRATION_SCHEMES} />
        </div>
        <div className="form__row">
          <TextField path={`${side}.electronicAddress`} label="Elektronisch adres (PEPPOL-id)" />
          <SelectField path={`${side}.electronicAddressScheme`} label="Schema elektronisch adres" options={ENDPOINT_SCHEMES} />
        </div>
        <div className="form__row">
          <TextField path={`${side}.address.streetName`} label="Straat en huisnummer" />
          <TextField path={`${side}.address.postalZone`} label="Postcode" />
        </div>
        <div className="form__row">
          <TextField path={`${side}.address.cityName`} label="Plaats" />
          <TextField path={`${side}.address.countryCode`} label="Landcode" placeholder="NL" />
        </div>
      </div>
    </div>
  );
}
