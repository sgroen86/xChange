"use client";

import { useRef, useState } from "react";

import { ApiError } from "../lib/api";
import { confirmMfaSetup, startMfaSetup, verifyMfa, type Session } from "../lib/auth";
import { text, useText } from "../lib/i18n";
import { isCompleteCode, normalizeCode } from "../lib/mfa";
import { Icon } from "./Icon";
import { MfaSetup } from "./MfaSetup";
import { useToast } from "./Toast";

/**
 * The second step of a login, after the password (or a confirmation link, or
 * an accepted invitation): a code from the app, or - when MFA is mandatory and
 * not set up yet - setting it up. Ends in a session, or sends the person back
 * to the start when the challenge expired, ran out of attempts or the address
 * is locked out.
 */
export function MfaSignIn({
  step,
  account,
  onSignedIn,
  onRestart,
}: {
  step: { kind: "mfa" | "mfaSetup"; challenge: string };
  /** The email address, for the recovery codes file. */
  account: string;
  onSignedIn: (session: Session) => void;
  onRestart: (message: string) => void;
}) {
  const t = useText();

  return (
    <div style={{ maxWidth: step.kind === "mfa" ? 480 : 560 }}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="user" /> {step.kind === "mfa" ? t.mfa.codeStep.title : t.mfa.setup.title}
          </h1>
          {step.kind === "mfa" && <p className="page-header__subtitle">{t.mfa.codeStep.subtitle}</p>}
        </div>
      </div>
      <div className="card">
        <div className="card__body">
          {step.kind === "mfa" ? (
            <CodeStep challenge={step.challenge} onSignedIn={onSignedIn} onRestart={onRestart} />
          ) : (
            <SetupStep challenge={step.challenge} account={account} onSignedIn={onSignedIn} onRestart={onRestart} />
          )}
        </div>
      </div>
    </div>
  );
}

function CodeStep({
  challenge,
  onSignedIn,
  onRestart,
}: {
  challenge: string;
  onSignedIn: (session: Session) => void;
  onRestart: (message: string) => void;
}) {
  const t = useText();
  const c = t.mfa.codeStep;
  const showToast = useToast();
  const [useRecovery, setUseRecovery] = useState(false);
  const [code, setCode] = useState("");
  const [recoveryCode, setRecoveryCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const ready = useRecovery ? recoveryCode.trim().length > 0 : isCompleteCode(code);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (busy || !ready) return;

    setBusy(true);
    setError(null);
    try {
      const session = await verifyMfa(challenge, useRecovery ? { recoveryCode: recoveryCode.trim() } : { code });
      if (typeof session.recoveryCodesLeft === "number") {
        showToast(text().mfa.codeStep.recoveryLeft(session.recoveryCodesLeft), "warning", 8000);
      }
      onSignedIn(session);
    } catch (cause) {
      const message = cause instanceof ApiError ? cause.message : text().auth.genericError;
      // 401: the challenge is over; 429: the address is locked out. Either way, back to the password.
      if (cause instanceof ApiError && (cause.status === 401 || cause.status === 429)) {
        onRestart(message);
        return;
      }
      setError(message);
      setCode("");
      setBusy(false);
    }
  };

  return (
    <form className="form" onSubmit={submit}>
      {useRecovery ? (
        <div className="form-group">
          <label className="form-label" htmlFor="mfa-recovery">
            {c.recoveryCode}
          </label>
          <input
            id="mfa-recovery"
            className="form-input text-mono"
            type="text"
            autoComplete="off"
            autoCapitalize="none"
            spellCheck={false}
            value={recoveryCode}
            onChange={(event) => setRecoveryCode(event.target.value)}
            required
          />
          <div className="form-help">
            <Icon name="info" /> {c.recoveryHelp}
          </div>
        </div>
      ) : (
        <div className="form-group">
          <label className="form-label" htmlFor="mfa-code">
            {c.code}
          </label>
          <input
            id="mfa-code"
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
      )}

      {error && (
        <div className="alert alert--danger" style={{ marginBottom: "var(--space-4)" }}>
          <Icon name="x" />
          <div>{error}</div>
        </div>
      )}

      <button type="submit" className="btn btn--primary" disabled={busy || !ready}>
        <Icon name="check" /> {busy ? t.common.busy : c.verify}
      </button>

      <p className="text-sm" style={{ marginTop: "var(--space-4)", display: "flex", gap: "var(--space-4)", flexWrap: "wrap" }}>
        <button
          type="button"
          className="btn btn--ghost btn--sm"
          onClick={() => {
            setUseRecovery(!useRecovery);
            setError(null);
          }}
        >
          {useRecovery ? c.useApp : c.useRecovery}
        </button>
        <button type="button" className="btn btn--ghost btn--sm" onClick={() => onRestart("")}>
          {c.back}
        </button>
      </p>
    </form>
  );
}

function SetupStep({
  challenge,
  account,
  onSignedIn,
  onRestart,
}: {
  challenge: string;
  account: string;
  onSignedIn: (session: Session) => void;
  onRestart: (message: string) => void;
}) {
  // The session arrives with the recovery codes, but is only handed on once they are saved.
  const session = useRef<Session | null>(null);

  return (
    <MfaSetup
      required
      account={account}
      start={() => startMfaSetup(challenge)}
      confirm={async (code) => {
        const result = await confirmMfaSetup(challenge, code);
        session.current = result.session;
        return result.recoveryCodes;
      }}
      onDone={() => {
        if (session.current) onSignedIn(session.current);
      }}
      onGone={onRestart}
    />
  );
}
