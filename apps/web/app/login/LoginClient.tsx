"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { MfaSignIn } from "../../components/MfaSignIn";
import { useToast } from "../../components/Toast";
import { ApiError, isApiConfigured } from "../../lib/api";
import { text, useText } from "../../lib/i18n";
import {
  isSetupRequired,
  listCountries,
  login,
  setupFirstUser,
  signupStatus,
  type CountryOption,
  type Session,
} from "../../lib/auth";

/**
 * Login, and first-run setup when no account exists yet — the same two states
 * the Bookkeeping app has (login.php and setup.php). xChange keeps its own
 * accounts; only the behaviour is shared.
 */
export default function LoginClient() {
  const router = useRouter();
  const showToast = useToast();
  const t = useText();

  const [mode, setMode] = useState<"loading" | "login" | "setup">("loading");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [name, setName] = useState("");
  const [organizationName, setOrganizationName] = useState("");
  const [countryCode, setCountryCode] = useState("");
  const [countries, setCountries] = useState<CountryOption[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [signupOpen, setSignupOpen] = useState(false);
  // After the right password: the code, or setting MFA up when it is mandatory.
  const [mfaStep, setMfaStep] = useState<{ kind: "mfa" | "mfaSetup"; challenge: string } | null>(null);

  useEffect(() => {
    let cancelled = false;

    signupStatus().then((status) => {
      if (!cancelled) setSignupOpen(status.open);
    });

    isSetupRequired()
      .then(async (required) => {
        if (cancelled) return;
        setMode(required ? "setup" : "login");
        if (required) {
          const loaded = await listCountries();
          if (!cancelled) setCountries(loaded);
        }
      })
      .catch(() => {
        if (!cancelled) setMode("login");
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (busy) return;

    setBusy(true);
    setError(null);

    try {
      const result =
        mode === "setup"
          ? await setupFirstUser(email, password, name, organizationName, countryCode)
          : await login(email, password);

      if (result.kind === "session") {
        signedIn(result.session);
      } else {
        setMfaStep(result);
      }
    } catch (cause) {
      const message =
        cause instanceof ApiError ? cause.message : t.auth.genericError;
      setError(message);
      showToast(message, "danger");
    } finally {
      setBusy(false);
    }
  };

  function signedIn(session: Session) {
    showToast(
      mode === "setup" ? text().auth.welcome(session.user.name) : text().auth.loginPage.welcomeBack(session.user.name),
      "success",
    );
    router.replace("/upload");
  }

  if (mfaStep) {
    return (
      <MfaSignIn
        step={mfaStep}
        account={email}
        onSignedIn={signedIn}
        onRestart={(message) => {
          setMfaStep(null);
          setPassword("");
          // First-run setup has created the account by now: what remains is logging in.
          setMode("login");
          setError(message || null);
        }}
      />
    );
  }

  if (!isApiConfigured) {
    return (
      <div className="card" style={{ maxWidth: 480 }}>
        <div className="card__body">
          <div className="alert alert--info">
            <Icon name="info" />
            <div>
              {t.auth.loginPage.noApi}
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (mode === "loading") {
    return (
      <div className="card" style={{ maxWidth: 480 }}>
        <div className="card__body">
          <p className="text-soft text-sm">{t.common.loading}</p>
        </div>
      </div>
    );
  }

  const isSetup = mode === "setup";

  return (
    <div style={{ maxWidth: 480 }}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="user" /> {isSetup ? t.auth.createAccount : t.auth.login}
          </h1>
          <p className="page-header__subtitle">
            {isSetup ? t.auth.loginPage.setupSubtitle : t.auth.loginPage.subtitle}
          </p>
        </div>
      </div>

      <div className="card">
        <div className="card__body">
          <form className="form" onSubmit={submit}>
            {isSetup && (
              <>
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
                  <label className="form-label" htmlFor="organization">
                    {t.auth.fields.organization}
                  </label>
                  <input
                    id="organization"
                    className="form-input"
                    type="text"
                    value={organizationName}
                    onChange={(event) => setOrganizationName(event.target.value)}
                    required
                  />
                </div>
                <div className="form-group">
                  <label className="form-label" htmlFor="country">
                    {t.auth.fields.country}
                  </label>
                  <select
                    id="country"
                    className="form-input"
                    value={countryCode}
                    onChange={(event) => setCountryCode(event.target.value)}
                    required
                  >
                    <option value="">{t.auth.fields.chooseCountry}</option>
                    {countries.map((country) => (
                      <option key={country.code} value={country.code}>
                        {country.name}
                      </option>
                    ))}
                  </select>
                </div>
              </>
            )}

            <div className="form-group">
              <label className="form-label" htmlFor="email">
                {t.auth.fields.email}
              </label>
              <input
                id="email"
                className="form-input"
                type="email"
                autoComplete="username"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
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
                autoComplete={isSetup ? "new-password" : "current-password"}
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
              />
              {isSetup && (
                <div className="form-help">
                  <Icon name="info" /> {t.auth.fields.passwordHelp}
                </div>
              )}
            </div>

            {error && (
              <div className="alert alert--danger" style={{ marginBottom: "var(--space-4)" }}>
                <Icon name="x" />
                <div>{error}</div>
              </div>
            )}

            <button type="submit" className="btn btn--primary" disabled={busy}>
              <Icon name="check" />
              {busy ? t.common.busy : isSetup ? t.auth.createAccount : t.auth.login}
            </button>
            {!isSetup && signupOpen && (
              <p className="text-soft text-sm" style={{ marginTop: "var(--space-4)" }}>
                {t.auth.loginPage.noAccount} <Link href="/signup">{t.auth.loginPage.signupFree}</Link>
              </p>
            )}
          </form>
        </div>
      </div>
    </div>
  );
}
