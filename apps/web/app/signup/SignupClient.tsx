"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { ApiError } from "../../lib/api";
import { currentLanguage, setLanguage, useLang, useText } from "../../lib/i18n";
import { listCountries, signupStatus, startSignup, type CountryOption } from "../../lib/auth";
import { formatMoney } from "../../lib/money";
import { SIGNUP_PLANS, forgetPlan, parseLang, parsePlan, rememberPlan, type SignupPlan } from "../../lib/signupPlan";

type State = { status: "loading" } | { status: "closed" } | { status: "open"; trial: number } | { status: "sent"; email: string };

/**
 * Self sign-up: details in, a confirmation mail out. Nothing exists until the
 * link in that mail is opened. Closed until the platform administrator opens it.
 *
 * The website links here with ?plan=<id>&lang=<nl|en>. The chosen plan is
 * shown and remembered in this browser, so Checkout can start for it once the
 * account is confirmed (see lib/signupPlan.ts and the settings page).
 */
export default function SignupClient() {
  const t = useText();
  const searchParams = useSearchParams();
  const urlLang = parseLang(searchParams.get("lang"));
  const planId = parsePlan(searchParams.get("plan"));
  const plan = planId ? SIGNUP_PLANS[planId] : null;
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

  // The website's language, when the app has it (French visitors get English).
  useEffect(() => {
    if (urlLang && urlLang !== currentLanguage()) setLanguage(urlLang);
  }, [urlLang]);

  // Arriving without a plan means the trial, also after an earlier choice.
  useEffect(() => {
    if (planId) rememberPlan(planId);
    else forgetPlan();
  }, [planId]);

  const startWithTrial = () => {
    const params = new URLSearchParams(searchParams.toString());
    params.delete("plan");
    const query = params.toString();
    window.history.replaceState(null, "", `${window.location.pathname}${query ? `?${query}` : ""}`);
  };

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
            {state.status !== "open" ? "xChange" : plan ? t.auth.signup.withPlan : t.auth.signup.trial(state.trial)}
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
            {plan && <ChosenPlan plan={plan} onRemove={startWithTrial} />}
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

function ChosenPlan({ plan, onRemove }: { plan: SignupPlan; onRemove: () => void }) {
  const t = useText();
  const s = t.auth.signup;
  const b = t.settings.billing;
  const lang = useLang();
  const count = plan.conversions.toLocaleString(lang === "en" ? "en-GB" : "nl-NL");

  return (
    <div className="stat-card" style={{ marginBottom: "var(--space-5)", borderColor: "var(--color-primary)" }}>
      <div className="stat-card__label">{plan.oneOff ? s.chosenCredits : s.chosenPlan}</div>
      <div className="stat-card__value">{plan.name ?? s.creditsName(plan.conversions)}</div>
      <p className="text-sm" style={{ margin: "var(--space-2) 0" }}>
        <strong>{formatMoney(plan.amount, plan.currency, lang)}</strong> {plan.oneOff ? b.oneOff : b.perMonth}
        {" · "}
        {plan.oneOff ? s.creditsNeverExpire : b.conversionsPerMonth(count)}
      </p>
      <div className="form-help">
        <Icon name="info" /> {s.payNext}
      </div>
      <button type="button" className="btn btn--ghost btn--sm" style={{ marginTop: "var(--space-3)" }} onClick={onRemove}>
        <Icon name="x" /> {s.useTrial}
      </button>
    </div>
  );
}
