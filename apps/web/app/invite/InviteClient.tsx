"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { MfaSignIn } from "../../components/MfaSignIn";
import { useToast } from "../../components/Toast";
import { ApiError } from "../../lib/api";
import { text, useText } from "../../lib/i18n";
import { acceptInvitation, getInvitation, listCountries, type InvitationDetails, type Session } from "../../lib/auth";

type State =
  | { status: "loading" }
  | { status: "ready"; invitation: InvitationDetails; countryName: string }
  | { status: "invalid"; message: string };

/**
 * Where an invited user sets up their own account: the organisation and email
 * are fixed by the invitation, the user chooses a password. Afterwards they
 * are Admin of the organisation and signed in.
 */
export default function InviteClient() {
  const router = useRouter();
  const showToast = useToast();
  const t = useText();
  const code = useSearchParams().get("code") ?? "";

  const [state, setState] = useState<State>({ status: "loading" });
  const [name, setName] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // The invited user becomes Admin, for whom MFA is mandatory: set up before the session.
  const [mfaStep, setMfaStep] = useState<{ kind: "mfa" | "mfaSetup"; challenge: string } | null>(null);

  useEffect(() => {
    let cancelled = false;

    Promise.all([getInvitation(code), listCountries()])
      .then(([invitation, countries]) => {
        if (cancelled) return;
        setName(invitation.name);
        setState({
          status: "ready",
          invitation,
          countryName: countries.find((c) => c.code === invitation.countryCode)?.name ?? invitation.countryCode,
        });
      })
      .catch((cause: unknown) => {
        if (!cancelled) {
          setState({
            status: "invalid",
            message: cause instanceof ApiError ? cause.message : text().auth.invite.invalid,
          });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [code]);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (busy) return;

    setBusy(true);
    setError(null);
    try {
      const result = await acceptInvitation(code, name, password);
      if (result.kind === "session") {
        signedIn(result.session);
      } else {
        setMfaStep(result);
      }
    } catch (cause) {
      const message = cause instanceof ApiError ? cause.message : t.auth.genericError;
      setError(message);
    } finally {
      setBusy(false);
    }
  };

  function signedIn(session: Session) {
    showToast(text().auth.welcome(session.user.name), "success");
    router.replace("/upload");
  }

  if (mfaStep && state.status === "ready") {
    return (
      <MfaSignIn
        step={mfaStep}
        account={state.invitation.email}
        onSignedIn={signedIn}
        // The invitation is used up and the account exists: what remains is an ordinary login.
        onRestart={() => router.replace("/login")}
      />
    );
  }

  return (
    <div style={{ maxWidth: 480 }}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="user" /> {t.auth.createAccount}
          </h1>
          <p className="page-header__subtitle">
            {state.status === "ready"
              ? t.auth.invite.invited(state.invitation.organizationName, state.countryName)
              : state.status === "loading"
                ? t.common.loading
                : t.auth.invite.title}
          </p>
        </div>
      </div>

      {state.status === "invalid" && (
        <div className="card">
          <div className="card__body">
            <div className="alert alert--warning">
              <Icon name="info" />
              <div>
                {state.message} {t.auth.invite.askAdmin}
              </div>
            </div>
            <div className="form__actions">
              <Link className="btn btn--secondary" href="/login">
                {t.auth.toLogin}
              </Link>
            </div>
          </div>
        </div>
      )}

      {state.status === "ready" && (
        <div className="card">
          <div className="card__body">
            <form className="form" onSubmit={submit}>
              <div className="form-group">
                <label className="form-label" htmlFor="email">
                  {t.auth.fields.email}
                </label>
                <input id="email" className="form-input" type="email" value={state.invitation.email} readOnly />
              </div>
              <div className="form-group">
                <label className="form-label" htmlFor="name">
                  {t.auth.fields.name}
                </label>
                <input
                  id="name"
                  className="form-input"
                  type="text"
                  autoComplete="name"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label" htmlFor="password">
                  {t.auth.fields.password}
                </label>
                <input
                  id="password"
                  className="form-input"
                  type="password"
                  autoComplete="new-password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  required
                />
                <div className="form-help">
                  <Icon name="info" /> {t.auth.fields.passwordHelp}
                </div>
              </div>

              {error && (
                <div className="alert alert--danger" style={{ marginBottom: "var(--space-4)" }}>
                  <Icon name="x" />
                  <div>{error}</div>
                </div>
              )}

              <button type="submit" className="btn btn--primary" disabled={busy}>
                <Icon name="check" /> {busy ? t.common.busy : t.auth.createAccount}
              </button>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
