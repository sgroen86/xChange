"use client";

import { useEffect, useState, useSyncExternalStore } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { isApiConfigured } from "../../lib/api";
import { getServerUserSnapshot, getUserSnapshot, listCountries, subscribeToUser, type CountryOption } from "../../lib/auth";
import { FALLBACK_SETTINGS, getSettings, updateSettings, type OrganizationSettings } from "../../lib/settings";
import { ENDPOINT_SCHEMES } from "../invoices/[invoiceId]/review/fields";
import { LearnedCorrections } from "./LearnedCorrections";

/**
 * The organisation's settings: the format the export menu opens on, and the
 * organisation's own PEPPOL id, which fills the seller's electronic address
 * on every invoice. Only an admin may change them; the API enforces that too.
 */
export default function SettingsClient() {
  const showToast = useToast();
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
    code ? (countries.find((c) => c.code === code)?.name ?? code) : "Nog niet vastgelegd";

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
        showToast(cause instanceof Error ? cause.message : "Laden is mislukt.", "danger");
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
      showToast("Instellingen opgeslagen.", "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : "Opslaan mislukt.", "danger");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div style={{ maxWidth: 720 }}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="settings" /> Instellingen
          </h1>
          <p className="page-header__subtitle">
            {settings === null ? "Laden…" : "Gelden voor iedereen in uw organisatie."}
          </p>
        </div>
      </div>

      {!isApiConfigured && (
        <div className="alert alert--info">
          <Icon name="info" />
          <div>Er is geen API geconfigureerd. Instellingen kunnen niet worden opgeslagen.</div>
        </div>
      )}

      {settings !== null && (
        <form className="form card" onSubmit={save}>
          <div className="card__body">
            <div className="form__row">
              <div className="form__group">
                <label htmlFor="organizationName">Organisatie</label>
                <input id="organizationName" type="text" value={settings.organizationName ?? "—"} readOnly />
              </div>
              <div className="form__group">
                <label htmlFor="countryCode">Land</label>
                <input id="countryCode" type="text" value={countryName(settings.countryCode)} readOnly />
              </div>
            </div>
            <div className="form-help" style={{ marginBottom: "var(--space-4)" }}>
              Naam en land wijzigt de platformbeheerder.
            </div>

            <div className="form__group">
              <label htmlFor="defaultRuleset">Standaard exportformaat</label>
              <select
                id="defaultRuleset"
                value={defaultRulesetId}
                disabled={!canEdit}
                onChange={(event) => setDefaultRulesetId(event.target.value)}
              >
                <option value="">
                  {settings.countryCode ? `Standaard voor ${countryName(settings.countryCode)}` : "Systeemstandaard"}
                  {/* Without an own choice, the effective default is the country's. */}
                  {!settings.defaultRulesetId &&
                    ` (${(settings.rulesets.find((r) => r.id === settings.effectiveDefaultRulesetId) ?? settings.rulesets[0])?.displayName})`}
                </option>
                {settings.rulesets.map((ruleset) => (
                  <option key={ruleset.id} value={ruleset.id}>
                    {ruleset.displayName} (regels {ruleset.rulesRelease})
                  </option>
                ))}
              </select>
              <div className="form-help">
                Het formaatmenu op een factuur opent hierop. Per download kunt u een ander kiezen.
              </div>
            </div>

            <div className="form__row">
              <div className="form__group">
                <label htmlFor="endpointId">Eigen elektronisch adres (PEPPOL-id)</label>
                <input
                  id="endpointId"
                  type="text"
                  value={endpointId}
                  readOnly={!canEdit}
                  onChange={(event) => setEndpointId(event.target.value)}
                />
              </div>
              <div className="form__group">
                <label htmlFor="endpointScheme">Schema</label>
                <select
                  id="endpointScheme"
                  value={endpointScheme}
                  disabled={!canEdit}
                  onChange={(event) => setEndpointScheme(event.target.value)}
                >
                  <option value="">—</option>
                  {ENDPOINT_SCHEMES.map((scheme) => (
                    <option key={scheme.value} value={scheme.value}>
                      {scheme.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>
            <div className="form-help">
              Wordt ingevuld als elektronisch adres van de verkoper op elke factuur waar dat nog
              ontbreekt.
            </div>

            {canEdit ? (
              <div className="form__actions">
                <button type="submit" className="btn btn--primary" disabled={saving || !isApiConfigured}>
                  <Icon name="save" /> {saving ? "Bezig…" : "Opslaan"}
                </button>
              </div>
            ) : (
              <p className="text-soft text-sm">Alleen een beheerder kan dit wijzigen.</p>
            )}
          </div>
        </form>
      )}

      {isApiConfigured && <LearnedCorrections canForget={user?.role === "Admin" || user?.role === "User"} />}
    </div>
  );
}
