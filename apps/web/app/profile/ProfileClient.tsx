"use client";

import { useCallback, useEffect, useState, useSyncExternalStore } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { isApiConfigured } from "../../lib/api";
import { getServerUserSnapshot, getUserSnapshot, listCountries, subscribeToUser, type CountryOption } from "../../lib/auth";
import { text, useText } from "../../lib/i18n";
import {
  FALLBACK_PROFILE,
  ProfileSaveError,
  getProfile,
  updateProfile,
  type CompanyDetails,
  type OrganizationProfile,
  type PeppolDetails,
} from "../../lib/profile";
import { legalNameToShow, tabOf, toWire, type FieldErrors, type ProfileTab } from "../../lib/profileForm";
import { endpointSchemes, paymentMeans, registrationSchemes, type Option } from "../invoices/[invoiceId]/review/fields";

type GeneralForm = Record<keyof CompanyDetails, string>;
type PeppolForm = Record<keyof PeppolDetails, string>;

function toForm<T extends object>(values: T): Record<keyof T, string> {
  return Object.fromEntries(Object.entries(values).map(([key, value]) => [key, (value as string | null) ?? ""])) as Record<keyof T, string>;
}

function fromForm<K extends string>(form: Record<K, string>): Record<K, string | null> {
  return Object.fromEntries(Object.entries<string>(form).map(([key, value]) => [key, toWire(value)])) as Record<K, string | null>;
}

/**
 * The organisation's company profile: a General tab with the seller details
 * every format uses, and a tab per format (PEPPOL first). Only an admin may
 * change it; the API enforces that too.
 */
export default function ProfileClient() {
  const showToast = useToast();
  const t = useText();
  const p = t.profile;
  const user = useSyncExternalStore(subscribeToUser, getUserSnapshot, getServerUserSnapshot);
  const canEdit = user?.role === "Admin";

  const [profile, setProfile] = useState<OrganizationProfile | null>(null);
  const [general, setGeneral] = useState<GeneralForm | null>(null);
  const [peppol, setPeppol] = useState<PeppolForm | null>(null);
  const [tab, setTab] = useState<ProfileTab>("general");
  const [errors, setErrors] = useState<FieldErrors>({});
  const [saving, setSaving] = useState(false);
  const [countries, setCountries] = useState<CountryOption[]>([]);

  const show = useCallback((loaded: OrganizationProfile) => {
    setProfile(loaded);
    setGeneral({ ...toForm(loaded.general), legalName: legalNameToShow(loaded.general.legalName, loaded.organizationName) });
    setPeppol(toForm(loaded.peppol));
  }, []);

  useEffect(() => {
    let cancelled = false;
    listCountries().then((loaded) => {
      if (!cancelled) setCountries(loaded);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    getProfile()
      .then((loaded) => {
        if (!cancelled) show(loaded);
      })
      .catch((cause: unknown) => {
        if (cancelled) return;
        show(FALLBACK_PROFILE);
        showToast(cause instanceof Error ? cause.message : text().common.loadFailed, "danger");
      });
    return () => {
      cancelled = true;
    };
  }, [show, showToast]);

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    if (saving || general === null || peppol === null) return;

    setSaving(true);
    setErrors({});
    try {
      show(await updateProfile({ general: fromForm(general), peppol: fromForm(peppol) }));
      showToast(p.saved, "success");
    } catch (cause) {
      if (cause instanceof ProfileSaveError) {
        setErrors(cause.fieldErrors);
        const first = Object.keys(cause.fieldErrors)[0];
        if (first) setTab(tabOf(first));
      }
      showToast(cause instanceof Error ? cause.message : p.saveFailed, "danger");
    } finally {
      setSaving(false);
    }
  };

  const setField = (block: ProfileTab, key: string, value: string) =>
    block === "general"
      ? setGeneral((current) => current && { ...current, [key]: value })
      : setPeppol((current) => current && { ...current, [key]: value });

  const valueOf = (block: ProfileTab, key: string) =>
    ((block === "general" ? general : peppol) as Record<string, string> | null)?.[key] ?? "";

  const errorOf = (id: string) =>
    errors[id] ? <div className="form-help text-danger">{errors[id]}</div> : null;

  const input = (block: ProfileTab, key: string, label: string, options?: { type?: string; help?: string }) => {
    const id = `${block}.${key}`;
    return (
      <div className="form__group">
        <label htmlFor={id}>{label}</label>
        <input
          id={id}
          type={options?.type ?? "text"}
          value={valueOf(block, key)}
          readOnly={!canEdit}
          className={errors[id] ? "input--invalid" : undefined}
          onChange={(event) => setField(block, key, event.target.value)}
        />
        {errorOf(id)}
        {options?.help && <div className="form-help">{options.help}</div>}
      </div>
    );
  };

  const select = (block: ProfileTab, key: string, label: string, choices: Option[]) => {
    const id = `${block}.${key}`;
    return (
      <div className="form__group">
        <label htmlFor={id}>{label}</label>
        <select
          id={id}
          value={valueOf(block, key)}
          disabled={!canEdit}
          className={errors[id] ? "input--invalid" : undefined}
          onChange={(event) => setField(block, key, event.target.value)}
        >
          <option value="">—</option>
          {choices.map((choice) => (
            <option key={choice.value} value={choice.value}>
              {choice.label}
            </option>
          ))}
        </select>
        {errorOf(id)}
      </div>
    );
  };

  const country = profile?.countryCode
    ? (countries.find((c) => c.code === profile.countryCode)?.name ?? profile.countryCode)
    : p.countryNotSet;

  const tabButton = (id: ProfileTab, label: string) => (
    <button type="button" className={`tab ${tab === id ? "tab--active" : ""}`.trim()} onClick={() => setTab(id)}>
      {label}
    </button>
  );

  return (
    <div style={{ maxWidth: 720 }}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="user" /> {p.title}
          </h1>
          <p className="page-header__subtitle">{profile === null ? t.common.loading : p.subtitle}</p>
        </div>
      </div>

      {!isApiConfigured && (
        <div className="alert alert--info">
          <Icon name="info" />
          <div>{p.noApi}</div>
        </div>
      )}

      {profile !== null && general !== null && peppol !== null && (
        <form className="form stack" onSubmit={save}>
          <div className="tabs">
            {tabButton("general", p.tabs.general)}
            {tabButton("peppol", p.tabs.peppol)}
          </div>

          <p className="text-soft text-sm">{p.howUsed}</p>

          {tab === "general" && (
            <>
              <div className="card">
                <div className="card__header">
                  <h3>{p.cards.company}</h3>
                </div>
                <div className="card__body">
                  {input("general", "legalName", p.fields.legalName, { help: p.legalNameHelp })}
                  {input("general", "vatIdentifier", p.fields.vatIdentifier)}
                  <div className="form__row">
                    {input("general", "legalRegistrationId", p.fields.legalRegistrationId)}
                    {select("general", "legalRegistrationScheme", p.fields.legalRegistrationScheme, registrationSchemes(t))}
                  </div>
                </div>
              </div>

              <div className="card">
                <div className="card__header">
                  <h3>{p.cards.address}</h3>
                </div>
                <div className="card__body">
                  {input("general", "streetName", p.fields.streetName)}
                  {input("general", "additionalStreetName", p.fields.additionalStreetName)}
                  <div className="form__row">
                    {input("general", "postalZone", p.fields.postalZone)}
                    {input("general", "cityName", p.fields.cityName)}
                  </div>
                  <div className="form__group">
                    <label htmlFor="general.country">{p.fields.country}</label>
                    <input id="general.country" type="text" value={country} readOnly />
                    <div className="form-help">{p.countryHelp}</div>
                  </div>
                </div>
              </div>

              <div className="card">
                <div className="card__header">
                  <h3>{p.cards.contact}</h3>
                </div>
                <div className="card__body">
                  {input("general", "contactName", p.fields.contactName)}
                  <div className="form__row">
                    {input("general", "contactEmail", p.fields.contactEmail, { type: "email" })}
                    {input("general", "contactPhone", p.fields.contactPhone, { type: "tel" })}
                  </div>
                </div>
              </div>

              <div className="card">
                <div className="card__header">
                  <h3>{p.cards.payment}</h3>
                </div>
                <div className="card__body">
                  {select("general", "paymentMeansCode", p.fields.paymentMeansCode, paymentMeans(t))}
                  {input("general", "iban", p.fields.iban)}
                  <div className="form__row">
                    {input("general", "bic", p.fields.bic)}
                    {input("general", "accountName", p.fields.accountName)}
                  </div>
                </div>
              </div>
            </>
          )}

          {tab === "peppol" && (
            <div className="card">
              <div className="card__header">
                <h3>{p.cards.peppol}</h3>
              </div>
              <div className="card__body">
                <p className="text-soft text-sm">{p.peppolIntro}</p>
                <div className="form__row">
                  {input("peppol", "endpointId", p.fields.endpointId)}
                  {select("peppol", "endpointScheme", p.fields.endpointScheme, endpointSchemes(t))}
                </div>
              </div>
            </div>
          )}

          {canEdit ? (
            <div className="form__actions">
              <button type="submit" className="btn btn--primary" disabled={saving || !isApiConfigured}>
                <Icon name="save" /> {saving ? t.common.busy : t.common.save}
              </button>
            </div>
          ) : (
            <p className="text-soft text-sm">{p.adminOnly}</p>
          )}
        </form>
      )}
    </div>
  );
}
