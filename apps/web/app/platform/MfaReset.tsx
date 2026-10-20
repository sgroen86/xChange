"use client";

import { useState } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { useText } from "../../lib/i18n";
import { resetUserMfa } from "../../lib/platform";

/**
 * For a user who lost both phone and recovery codes: the platform
 * administrator turns their two-step verification off. There is no user list
 * on the platform page, so the user is named by email address.
 */
export function MfaReset() {
  const t = useText();
  const r = t.mfa.reset;
  const showToast = useToast();
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    const address = email.trim();
    if (busy || !address || !window.confirm(r.confirm(address))) return;

    setBusy(true);
    try {
      await resetUserMfa(address);
      showToast(r.done(address), "success");
      setEmail("");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : t.common.saveFailed, "danger");
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="card form" onSubmit={submit} style={{ marginBottom: "var(--space-4)" }}>
      <div className="card__header">
        <h3>{r.title}</h3>
      </div>
      <div className="card__body">
        <div className="form__row">
          <div className="form__group">
            <label htmlFor="mfa-reset-email">{r.email}</label>
            <input id="mfa-reset-email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
          </div>
          <div className="form__group" style={{ justifyContent: "flex-end" }}>
            <button type="submit" className="btn btn--danger" disabled={busy || !email.trim()}>
              <Icon name="x" /> {busy ? t.common.busy : r.submit}
            </button>
          </div>
        </div>
        <div className="form-help">{r.help}</div>
      </div>
    </form>
  );
}
