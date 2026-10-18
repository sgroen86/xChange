"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { ApiError } from "../../lib/api";
import { listCountries, signupStatus, startSignup, type CountryOption } from "../../lib/auth";

type State = { status: "loading" } | { status: "closed" } | { status: "open"; trial: number } | { status: "sent"; email: string };

/**
 * Self sign-up: details in, a confirmation mail out. Nothing exists until the
 * link in that mail is opened. Closed until the platform administrator opens it.
 */
export default function SignupClient() {
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
      setError(cause instanceof ApiError ? cause.message : "Er ging iets mis. Probeer het opnieuw.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div style={{ maxWidth: 520 }}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="user" /> Aanmelden
          </h1>
          <p className="page-header__subtitle">
            {state.status === "open" ? `Probeer xChange gratis: ${state.trial} conversies, geen betaalgegevens nodig.` : "xChange"}
          </p>
        </div>
      </div>

      {state.status === "loading" && <p className="text-soft text-sm">Laden&hellip;</p>}

      {state.status === "closed" && (
        <div className="card">
          <div className="card__body">
            <div className="alert alert--info">
              <Icon name="info" />
              <div>Aanmelden is nog niet open. Heb je al een account?</div>
            </div>
            <div className="form__actions">
              <Link className="btn btn--secondary" href="/login">
                Naar inloggen
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
                We hebben een e-mail gestuurd naar <strong>{state.email}</strong>. Open de link daarin om je
                account te activeren; hij is 24 uur geldig. Geen mail gezien? Kijk ook in je spammap.
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
                <label className="form-label" htmlFor="name">Je naam</label>
                <input id="name" className="form-input" autoComplete="name" value={form.name} onChange={set("name")} required />
              </div>
              <div className="form-group">
                <label className="form-label" htmlFor="email">E-mailadres</label>
                <input id="email" className="form-input" type="email" autoComplete="email" value={form.email} onChange={set("email")} required />
              </div>
              <div className="form-group">
                <label className="form-label" htmlFor="password">Wachtwoord</label>
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
                  <Icon name="info" /> Minimaal 12 tekens.
                </div>
              </div>
              <div className="form-group">
                <label className="form-label" htmlFor="organizationName">Bedrijfsnaam</label>
                <input id="organizationName" className="form-input" autoComplete="organization" value={form.organizationName} onChange={set("organizationName")} required />
              </div>
              <div className="form-group">
                <label className="form-label" htmlFor="countryCode">Land</label>
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
                <Icon name="check" /> {busy ? "Bezig…" : "Account aanmaken"}
              </button>
              <p className="text-soft text-sm" style={{ marginTop: "var(--space-4)" }}>
                Al een account? <Link href="/login">Inloggen</Link>
              </p>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
