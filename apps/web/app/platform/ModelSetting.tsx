"use client";

import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { useText } from "../../lib/i18n";
import { getPlatformSettings, updatePlatformSettings, type PlatformSettings } from "../../lib/platform";

/**
 * Which Claude model reads the PDFs, for the whole platform. Every
 * extraction logs its token counts, so the effect of a change on cost can be
 * read from real invoices.
 */
export function ModelSetting() {
  const showToast = useToast();
  const t = useText();
  const m = t.platform.model;
  const [settings, setSettings] = useState<PlatformSettings | null>(null);
  const [choice, setChoice] = useState("");
  const [signupOpen, setSignupOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;
    getPlatformSettings()
      .then((loaded) => {
        if (cancelled) return;
        setSettings(loaded);
        setChoice(loaded.extractionModel ?? "");
        setSignupOpen(loaded.signupOpen);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  if (!settings) return null;

  const save = async () => {
    setSaving(true);
    try {
      const saved = await updatePlatformSettings(choice || null, signupOpen);
      setSettings(saved);
      showToast(m.saved, "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : t.common.saveFailed, "danger");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="card form" style={{ marginBottom: "var(--space-4)" }}>
      <div className="card__header">
        <h3>{m.title}</h3>
      </div>
      <div className="card__body">
        <div className="form__row">
          <div className="form__group">
            <label htmlFor="extractionModel">{m.label}</label>
            <select id="extractionModel" value={choice} onChange={(event) => setChoice(event.target.value)}>
              <option value="">{m.configDefault}</option>
              {settings.models.map((model) => (
                <option key={model.id} value={model.id}>
                  {model.name}
                </option>
              ))}
            </select>
          </div>
          <div className="form__group" style={{ justifyContent: "flex-end" }}>
            <button
              type="button"
              className="btn btn--primary"
              onClick={() => void save()}
              disabled={saving || (choice === (settings.extractionModel ?? "") && signupOpen === settings.signupOpen)}
            >
              <Icon name="save" /> {saving ? t.common.busy : t.common.save}
            </button>
          </div>
        </div>
        <div className="form-help">{m.help}</div>
        <label className="checkbox" style={{ display: "flex", gap: "var(--space-2)", alignItems: "center", marginTop: "var(--space-4)" }}>
          <input type="checkbox" checked={signupOpen} onChange={(event) => setSignupOpen(event.target.checked)} />
          {m.signupOpen}
        </label>
        <div className="form-help">{m.signupHelp}</div>
      </div>
    </div>
  );
}
