"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { ApiError, isApiConfigured } from "../../lib/api";
import { isSetupRequired, login, setupFirstUser } from "../../lib/auth";

/**
 * Login, and first-run setup when no account exists yet — the same two states
 * the Bookkeeping app has (login.php and setup.php). xChange keeps its own
 * accounts; only the behaviour is shared.
 */
export default function LoginClient() {
  const router = useRouter();
  const showToast = useToast();

  const [mode, setMode] = useState<"loading" | "login" | "setup">("loading");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [name, setName] = useState("");
  const [organizationName, setOrganizationName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;

    isSetupRequired()
      .then((required) => {
        if (!cancelled) setMode(required ? "setup" : "login");
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
      if (mode === "setup") {
        const session = await setupFirstUser(email, password, name, organizationName);
        showToast(`Welkom, ${session.user.name}.`, "success");
      } else {
        const session = await login(email, password);
        showToast(`Welkom terug, ${session.user.name}.`, "success");
      }

      router.replace("/upload");
    } catch (cause) {
      const message =
        cause instanceof ApiError ? cause.message : "Er ging iets mis. Probeer het opnieuw.";
      setError(message);
      showToast(message, "danger");
    } finally {
      setBusy(false);
    }
  };

  if (!isApiConfigured) {
    return (
      <div className="card" style={{ maxWidth: 480 }}>
        <div className="card__body">
          <div className="alert alert--info">
            <Icon name="info" />
            <div>
              Er is geen API geconfigureerd, dus inloggen is niet nodig. De app draait op mockdata.
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
          <p className="text-soft text-sm">Laden&hellip;</p>
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
            <Icon name="user" /> {isSetup ? "Account aanmaken" : "Inloggen"}
          </h1>
          <p className="page-header__subtitle">
            {isSetup
              ? "Er bestaat nog geen account. Maak het eerste beheerdersaccount aan."
              : "Log in om facturen te verwerken."}
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
                    Naam
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
                    Organisatie
                  </label>
                  <input
                    id="organization"
                    className="form-input"
                    type="text"
                    value={organizationName}
                    onChange={(event) => setOrganizationName(event.target.value)}
                  />
                </div>
              </>
            )}

            <div className="form-group">
              <label className="form-label" htmlFor="email">
                E-mailadres
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
                Wachtwoord
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
                  <Icon name="info" /> Minimaal 12 tekens.
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
              {busy ? "Bezig…" : isSetup ? "Account aanmaken" : "Inloggen"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
