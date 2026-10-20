"use client";

import { useEffect, useState, useSyncExternalStore } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { isApiConfigured } from "../../lib/api";
import { getServerUserSnapshot, getUserSnapshot, listCountries, subscribeToUser, type CountryOption } from "../../lib/auth";
import { text, useText } from "../../lib/i18n";
import { FALLBACK_SETTINGS, getSettings, updateSettings, type OrganizationSettings } from "../../lib/settings";
import { endpointSchemes } from "../invoices/[invoiceId]/review/fields";
import { InboxAddress } from "./InboxAddress";
import { LearnedCorrections } from "./LearnedCorrections";
import { TwoStepCard } from "./TwoStepCard";
import { UsageCard } from "./UsageCard";

/**
 * The organisation's settings: the format the export menu opens on, and the
 * organisation's own PEPPOL id, which fills the seller's electronic address
 * on every invoice. Only an admin may change them; the API enforces that too.
 */
export default function SettingsClient() {
  const showToast = useToast();
  const t = useText();
  const user = useSyncExternalStore(subscribeToUser, getUserSnapshot, getServerUserSnapshot);
  const canEdit = user?.role === "Admin";

  const [settings, setSettings] = useState<OrganizationSettings | null>(null);
  const [defaultRulesetId, setDefaultRulesetId] = useState("");
  const [endpointId, setEndpointId] = useState("");
  const [endpointScheme, setEndpointScheme] = useState("");
  const [saving, setSaving] = useState(false);
  const [countries, setCountries] = useState<CountryOption[]>([]);

  useEffect(() => {
    let cancelled = false;
    listCountries().then((loaded) => {
      if (!cancelled) setCountries(loaded);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  const countryName = (code: string | null | undefined) =>
    code ? (countries.find((c) => c.code === code)?.name ?? code) : t.settings.countryNotSet;

  useEffect(() => {
    let cancelled = false;
    getSettings()
      .then((loaded) => {
        if (cancelled) return;
        setSettings(loaded);
        setDefaultRulesetId(loaded.defaultRulesetId ?? "");
        setEndpointId(loaded.endpointId ?? "");
        setEndpointScheme(loaded.endpointScheme ?? "");
      })
      .catch((cause: unknown) => {
        if (cancelled) return;
        setSettings(FALLBACK_SETTINGS);
        showToast(cause instanceof Error ? cause.message : text().common.loadFailed, "danger");
      });
    return () => {
      cancelled = true;
    };
  }, [showToast]);

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    if (saving) return;

    setSaving(true);
    try {
      const saved = await updateSettings({
        defaultRulesetId: defaultRulesetId || null,
        endpointId: endpointId || null,
        endpointScheme: endpointScheme || null,
      });
      setSettings(saved);
      showToast(t.settings.saved, "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : t.settings.saveFailed, "danger");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div style={{ maxWidth: 720 }}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="settings" /> {t.settings.title}
          </h1>
          <p className="page-header__subtitle">
            {settings === null ? t.common.loading : t.settings.subtitle}
          </p>
        </div>
      </div>

      {!isApiConfigured && (
        <div className="alert alert--info">
          <Icon name="info" />
          <div>{t.settings.noApi}</div>
        </div>
      )}

      {settings !== null && (
        <form className="form card" onSubmit={save}>
          <div className="card__body">
            <div className="form__row">
              <div className="form__group">
                <label htmlFor="organizationName">{t.settings.organization}</label>
                <input id="organizationName" type="text" value={settings.organizationName ?? "—"} readOnly />
              </div>
              <div className="form__group">
                <label htmlFor="countryCode">{t.settings.country}</label>
                <input id="countryCode" type="text" value={countryName(settings.countryCode)} readOnly />
              </div>
            </div>
            <div className="form-help" style={{ marginBottom: "var(--space-4)" }}>
              {t.settings.nameAndCountryHelp}
            </div>

            <div className="form__group">
              <label htmlFor="defaultRuleset">{t.settings.defaultFormat}</label>
              <select
                id="defaultRuleset"
                value={defaultRulesetId}
                disabled={!canEdit}
                onChange={(event) => setDefaultRulesetId(event.target.value)}
              >
                <option value="">
                  {settings.countryCode ? t.settings.defaultFor(countryName(settings.countryCode)) : t.settings.systemDefault}
                  {/* Without an own choice, the effective default is the country's. */}
                  {!settings.defaultRulesetId &&
                    ` (${(settings.rulesets.find((r) => r.id === settings.effectiveDefaultRulesetId) ?? settings.rulesets[0])?.displayName})`}
                </option>
                {settings.rulesets.map((ruleset) => (
                  <option key={ruleset.id} value={ruleset.id}>
                    {t.settings.rulesetOption(ruleset.displayName, ruleset.rulesRelease)}
                  </option>
                ))}
              </select>
              <div className="form-help">{t.settings.formatHelp}</div>
            </div>

            <div className="form__row">
              <div className="form__group">
                <label htmlFor="endpointId">{t.settings.endpointId}</label>
                <input
                  id="endpointId"
                  type="text"
                  value={endpointId}
                  readOnly={!canEdit}
                  onChange={(event) => setEndpointId(event.target.value)}
                />
              </div>
              <div className="form__group">
                <label htmlFor="endpointScheme">{t.settings.endpointScheme}</label>
                <select
                  id="endpointScheme"
                  value={endpointScheme}
                  disabled={!canEdit}
                  onChange={(event) => setEndpointScheme(event.target.value)}
                >
                  <option value="">—</option>
                  {endpointSchemes(t).map((scheme) => (
                    <option key={scheme.value} value={scheme.value}>
                      {scheme.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>
            <div className="form-help">{t.settings.endpointHelp}</div>

            {canEdit ? (
              <div className="form__actions">
                <button type="submit" className="btn btn--primary" disabled={saving || !isApiConfigured}>
                  <Icon name="save" /> {saving ? t.common.busy : t.common.save}
                </button>
              </div>
            ) : (
              <p className="text-soft text-sm">{t.settings.adminOnly}</p>
            )}
          </div>
        </form>
      )}

      {isApiConfigured && user && <TwoStepCard email={user.email} />}

      <UsageCard />

      <InboxAddress canEdit={canEdit} />

      {isApiConfigured && <LearnedCorrections canForget={user?.role === "Admin" || user?.role === "User"} />}
    </div>
  );
}
