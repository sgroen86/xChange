"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { ApiError } from "../../lib/api";
import { useText } from "../../lib/i18n";
import { listCountries, signupStatus, startSignup, type CountryOption } from "../../lib/auth";

type State = { status: "loading" } | { status: "closed" } | { status: "open"; trial: number } | { status: "sent"; email: string };

/**
 * Self sign-up: details in, a confirmation mail out. Nothing exists until the
 * link in that mail is opened. Closed until the platform administrator opens it.
 */
export default function SignupClient() {
  const t = useText();
  const [state, setState] = useState<State>({ status: "loading" });
  const [countries, setCountries] = useState<CountryOption[]>([]);
  const [form, setForm] = useState({ name: "", email: "", password: "", organizationName: "", countryCode: "NL" });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    Promise.all([signupStatus(), listCountries()]).then(([status, list]) => {
      if (cancelled) return;
      setCountries(list);
      setState(status.open ? { status: "open", trial: status.trialConversions } : { status: "closed" });
    });
    return () => {
      cancelled = true;
    };
  }, []);

  const set = (key: keyof typeof form) => (event: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm((current) => ({ ...current, [key]: event.target.value }));

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await startSignup(form);
      setState({ status: "sent", email: form.email.trim() });
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : t.auth.genericError);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div style={{ maxWidth: 520 }}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="user" /> {t.auth.signup.title}
          </h1>
          <p className="page-header__subtitle">
            {state.status === "open" ? t.auth.signup.trial(state.trial) : "xChange"}
          </p>
        </div>
      </div>

      {state.status === "loading" && <p className="text-soft text-sm">{t.common.loading}</p>}

      {state.status === "closed" && (
        <div className="card">
          <div className="card__body">
            <div className="alert alert--info">
              <Icon name="info" />
              <div>{t.auth.signup.closed}</div>
            </div>
            <div className="form__actions">
              <Link className="btn btn--secondary" href="/login">
                {t.auth.toLogin}
              </Link>
            </div>
          </div>
        </div>
      )}

      {state.status === "sent" && (
        <div className="card">
          <div className="card__body">
            <div className="alert alert--success">
              <Icon name="check" />
              <div>
                {t.auth.signup.sentTo} <strong>{state.email}</strong>. {t.auth.signup.sentNext}
              </div>
            </div>
          </div>
        </div>
      )}

      {state.status === "open" && (
        <div className="card">
          <div className="card__body">
            <form className="form" onSubmit={submit}>
              <div className="form-group">
                <label className="form-label" htmlFor="name">{t.auth.fields.yourName}</label>
                <input id="name" className="form-input" autoComplete="name" value={form.name} onChange={set("name")} required />
              </div>
              <div className="form-group">
                <label className="form-label" htmlFor="email">{t.auth.fields.email}</label>
                <input id="email" className="form-input" type="email" autoComplete="email" value={form.email} onChange={set("email")} required />
              </div>
              <div className="form-group">
                <label className="form-label" htmlFor="password">{t.auth.fields.password}</label>
                <input
                  id="password"
                  className="form-input"
                  type="password"
                  autoComplete="new-password"
                  value={form.password}
                  onChange={set("password")}
                  required
                  minLength={12}
                />
                <div className="form-help">
                  <Icon name="info" /> {t.auth.fields.passwordHelp}
                </div>
              </div>
              <div className="form-group">
                <label className="form-label" htmlFor="organizationName">{t.auth.fields.companyName}</label>
                <input id="organizationName" className="form-input" autoComplete="organization" value={form.organizationName} onChange={set("organizationName")} required />
              </div>
              <div className="form-group">
                <label className="form-label" htmlFor="countryCode">{t.auth.fields.country}</label>
                <select id="countryCode" className="form-input" value={form.countryCode} onChange={set("countryCode")}>
                  {countries.map((country) => (
                    <option key={country.code} value={country.code}>
                      {country.name}
                    </option>
                  ))}
                </select>
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
              <p className="text-soft text-sm" style={{ marginTop: "var(--space-4)" }}>
                {t.auth.signup.haveAccount} <Link href="/login">{t.auth.login}</Link>
              </p>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
