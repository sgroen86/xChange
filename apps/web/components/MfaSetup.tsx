"use client";

import { toString as qrToSvg } from "qrcode";
import { useEffect, useRef, useState } from "react";

import { ApiError } from "../lib/api";
import type { MfaSetupInfo } from "../lib/auth";
import { text, useText } from "../lib/i18n";
import { formatSecret, isCompleteCode, normalizeCode } from "../lib/mfa";
import { Icon } from "./Icon";
import { RecoveryCodes } from "./RecoveryCodes";

type Stage =
  | { name: "preparing" }
  | { name: "scan"; setup: MfaSetupInfo; qr: string | null }
  | { name: "codes"; codes: string[] };

/**
 * Setting up an authenticator app: an explanation, the QR code of the
 * otpauth URI (drawn here in the browser - the static site has no server),
 * the secret as text for typing in by hand, the first code to confirm, and
 * then the recovery codes, once. Used during login (when MFA is mandatory)
 * and from the settings.
 */
export function MfaSetup({
  required,
  account,
  start,
  confirm,
  onDone,
  onGone,
}: {
  /** Mandatory for this user: the intro says so. */
  required: boolean;
  account: string;
  start: () => Promise<MfaSetupInfo>;
  /** Confirms the first code; resolves to the recovery codes. */
  confirm: (code: string) => Promise<string[]>;
  /** After the recovery codes were saved. */
  onDone: () => void;
  /** The login challenge ended (expired, used up, locked out): start the login again. */
  onGone?: (message: string) => void;
}) {
  const t = useText();
  const s = t.mfa.setup;
  const [stage, setStage] = useState<Stage>({ name: "preparing" });
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  // Starting creates a secret; React's development double-run must not create two.
  const started = useRef(false);

  const fail = (cause: unknown) => {
    const message = cause instanceof ApiError ? cause.message : text().auth.genericError;
    if (onGone && cause instanceof ApiError && (cause.status === 401 || cause.status === 429)) {
      onGone(message);
      return;
    }
    setError(message);
  };

  useEffect(() => {
    if (started.current) return;
    started.current = true;

    start()
      .then(async (setup) => {
        setStage({ name: "scan", setup, qr: null });
        const svg = await qrToSvg(setup.otpauthUri, { type: "svg", margin: 2, errorCorrectionLevel: "M" });
        setStage({ name: "scan", setup, qr: `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}` });
      })
      .catch(fail);
    // fail and start are stable for the life of this step.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (busy || !isCompleteCode(code)) return;

    setBusy(true);
    setError(null);
    try {
      setStage({ name: "codes", codes: await confirm(code) });
    } catch (cause) {
      setCode("");
      fail(cause);
    } finally {
      setBusy(false);
    }
  };

  if (stage.name === "codes") {
    return <RecoveryCodes codes={stage.codes} account={account} onContinue={onDone} />;
  }

  return (
    <div>
      <p className="text-sm" style={{ marginBottom: "var(--space-4)" }}>
        {required ? s.requiredIntro : s.optionalIntro}
      </p>

      {stage.name === "preparing" ? (
        !error && <p className="text-soft text-sm">{s.preparing}</p>
      ) : (
        <form className="form" onSubmit={submit}>
          <p className="text-sm font-medium">{s.scan}</p>
          <div
            style={{
              width: 196,
              height: 196,
              background: "#ffffff",
              borderRadius: 10,
              padding: "var(--space-2)",
              margin: "var(--space-3) 0",
            }}
          >
            {/* A data URI drawn in the browser: there is nothing for next/image to optimise. */}
            {/* eslint-disable-next-line @next/next/no-img-element */}
            {stage.qr && <img src={stage.qr} alt={s.qrAlt} width={180} height={180} />}
          </div>
          <p className="text-sm text-soft">{s.manual}</p>
          <p className="text-mono" style={{ margin: "var(--space-2) 0 var(--space-4)", userSelect: "all", wordBreak: "break-all" }}>
            {formatSecret(stage.setup.secret)}
          </p>

          <div className="form-group">
            <label className="form-label" htmlFor="mfa-setup-code">
              {s.enterCode}
            </label>
            <input
              id="mfa-setup-code"
              className="form-input text-mono"
              type="text"
              inputMode="numeric"
              autoComplete="one-time-code"
              maxLength={6}
              value={code}
              onChange={(event) => setCode(normalizeCode(event.target.value))}
              style={{ maxWidth: 160, letterSpacing: "0.2em" }}
              required
            />
          </div>

          {error && (
            <div className="alert alert--danger" style={{ marginBottom: "var(--space-4)" }}>
              <Icon name="x" />
              <div>{error}</div>
            </div>
          )}

          <button type="submit" className="btn btn--primary" disabled={busy || !isCompleteCode(code)}>
            <Icon name="check" /> {busy ? t.common.busy : s.confirm}
          </button>
        </form>
      )}

      {stage.name === "preparing" && error && (
        <div className="alert alert--danger">
          <Icon name="x" />
          <div>{error}</div>
        </div>
      )}
    </div>
  );
}
