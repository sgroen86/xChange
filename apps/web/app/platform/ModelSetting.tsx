"use client";

import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { getPlatformSettings, updatePlatformSettings, type PlatformSettings } from "../../lib/platform";

/**
 * Which Claude model reads the PDFs, for the whole platform. Every
 * extraction logs its token counts, so the effect of a change on cost can be
 * read from real invoices.
 */
export function ModelSetting() {
  const showToast = useToast();
  const [settings, setSettings] = useState<PlatformSettings | null>(null);
  const [choice, setChoice] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;
    getPlatformSettings()
      .then((loaded) => {
        if (cancelled) return;
        setSettings(loaded);
        setChoice(loaded.extractionModel ?? "");
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
      const saved = await updatePlatformSettings(choice || null);
      setSettings(saved);
      showToast("Leesmodel opgeslagen.", "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : "Opslaan is mislukt.", "danger");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="card form" style={{ marginBottom: "var(--space-4)" }}>
      <div className="card__header">
        <h3>Leesmodel</h3>
      </div>
      <div className="card__body">
        <div className="form__row">
          <div className="form__group">
            <label htmlFor="extractionModel">Model dat de PDF&apos;s leest</label>
            <select id="extractionModel" value={choice} onChange={(event) => setChoice(event.target.value)}>
              <option value="">Standaard uit de configuratie</option>
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
              disabled={saving || choice === (settings.extractionModel ?? "")}
            >
              <Icon name="save" /> {saving ? "Bezig…" : "Opslaan"}
            </button>
          </div>
        </div>
        <div className="form-help">
          Geldt voor alle organisaties, voor elke volgende upload en import. Het aantal tokens per
          factuur staat in de logboeken, zodat het effect op de kosten te meten is.
        </div>
      </div>
    </div>
  );
}
