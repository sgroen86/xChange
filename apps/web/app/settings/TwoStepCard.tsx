"use client";

import { useCallback, useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { MfaSetup } from "../../components/MfaSetup";
import { RecoveryCodes } from "../../components/RecoveryCodes";
import { useToast } from "../../components/Toast";
import {
  confirmAccountMfa,
  disableMfa,
  getMfaStatus,
  regenerateRecoveryCodes,
  startAccountMfaSetup,
  type MfaStatus,
} from "../../lib/accountMfa";
import { isApiConfigured } from "../../lib/api";
import { useText } from "../../lib/i18n";

type Panel =
  | { kind: "none" }
  | { kind: "setup" }
  | { kind: "regenerate" }
  | { kind: "disable" }
  | { kind: "codes"; codes: string[] };

/**
 * Two-step verification for the signed-in user: whether it is on, turning it
 * on, new recovery codes, and turning it off - which is not offered (and the
 * API refuses it) where it is mandatory.
 */
export function TwoStepCard({ email }: { email: string }) {
  const t = useText();
  const m = t.mfa.settings;
  const showToast = useToast();
  const [status, setStatus] = useState<MfaStatus | null>(null);
  const [panel, setPanel] = useState<Panel>({ kind: "none" });
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);

  const reload = useCallback(
    () =>
      getMfaStatus()
        .then(setStatus)
        .catch(() => setStatus(null)),
    [],
  );

  useEffect(() => {
    if (!isApiConfigured) return;
    let cancelled = false;
    getMfaStatus()
      .then((loaded) => {
        if (!cancelled) setStatus(loaded);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  if (!status) return null;

  const open = (next: Panel) => {
    setCode("");
    setPanel(next);
  };

  const submitCode = async (event: React.FormEvent) => {
    event.preventDefault();
    if (busy || !code.trim()) return;

    setBusy(true);
    try {
      if (panel.kind === "regenerate") {
        const codes = await regenerateRecoveryCodes(code.trim());
        showToast(m.renewed, "success");
        setPanel({ kind: "codes", codes });
      } else {
        await disableMfa(code.trim());
        showToast(m.disabled, "success");
        setPanel({ kind: "none" });
        await reload();
      }
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : t.auth.genericError, "danger");
      setCode("");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="card" style={{ marginTop: "var(--space-4)" }}>
      <div className="card__header">
        <h3>{m.title}</h3>
        <div style={{ display: "flex", gap: "var(--space-2)" }}>
          {status.mandatory && <span className="badge badge--info">{m.mandatory}</span>}
          <span className={`badge ${status.enabled ? "badge--success" : "badge--neutral"}`}>{status.enabled ? m.on : m.off}</span>
        </div>
      </div>
      <div className="card__body">
        <p className="text-sm">{status.enabled ? m.statusOn(status.recoveryCodesLeft) : m.statusOff}</p>
        <p className="text-sm text-soft" style={{ marginBottom: "var(--space-4)" }}>
          {status.mandatory ? m.mandatoryHelp : m.help}
        </p>

        {panel.kind === "none" && (
          <div style={{ display: "flex", gap: "var(--space-2)", flexWrap: "wrap" }}>
            {!status.enabled && (
              <button type="button" className="btn btn--primary" onClick={() => open({ kind: "setup" })}>
                <Icon name="check" /> {m.enable}
              </button>
            )}
            {status.enabled && (
              <button type="button" className="btn btn--secondary" onClick={() => open({ kind: "regenerate" })}>
                <Icon name="edit" /> {m.regenerate}
              </button>
            )}
            {status.enabled && !status.mandatory && (
              <button type="button" className="btn btn--ghost" onClick={() => open({ kind: "disable" })}>
                <Icon name="x" /> {m.disable}
              </button>
            )}
          </div>
        )}

        {panel.kind === "setup" && (
          <MfaSetup
            required={status.mandatory}
            account={email}
            start={startAccountMfaSetup}
            confirm={confirmAccountMfa}
            onDone={() => {
              showToast(m.enabled, "success");
              setPanel({ kind: "none" });
              void reload();
            }}
          />
        )}

        {panel.kind === "codes" && (
          <RecoveryCodes
            codes={panel.codes}
            account={email}
            onContinue={() => {
              setPanel({ kind: "none" });
              void reload();
            }}
          />
        )}

        {(panel.kind === "regenerate" || panel.kind === "disable") && (
          <form className="form" onSubmit={submitCode}>
            <p className="text-sm text-soft">{panel.kind === "regenerate" ? m.regenerateHelp : m.disableHelp}</p>
            <div className="form__group">
              <label htmlFor="mfa-settings-code">{m.codeOrRecovery}</label>
              <input
                id="mfa-settings-code"
                type="text"
                className="text-mono"
                autoComplete="one-time-code"
                autoCapitalize="none"
                spellCheck={false}
                value={code}
                onChange={(event) => setCode(event.target.value)}
                style={{ maxWidth: 240 }}
                required
              />
            </div>
            <div className="form__actions">
              <button
                type="submit"
                className={`btn ${panel.kind === "disable" ? "btn--danger" : "btn--primary"}`}
                disabled={busy || !code.trim()}
              >
                <Icon name={panel.kind === "disable" ? "x" : "check"} />{" "}
                {busy ? t.common.busy : panel.kind === "disable" ? m.disable : m.regenerate}
              </button>
              <button type="button" className="btn btn--secondary" onClick={() => open({ kind: "none" })}>
                {t.common.cancel}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
