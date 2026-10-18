"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { ApiError } from "../../lib/api";
import { acceptInvitation, getInvitation, listCountries, type InvitationDetails } from "../../lib/auth";

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
  const code = useSearchParams().get("code") ?? "";

  const [state, setState] = useState<State>({ status: "loading" });
  const [name, setName] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

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
            message: cause instanceof ApiError ? cause.message : "Deze uitnodiging is ongeldig of verlopen.",
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
      const session = await acceptInvitation(code, name, password);
      showToast(`Welkom, ${session.user.name}.`, "success");
      router.replace("/upload");
    } catch (cause) {
      const message = cause instanceof ApiError ? cause.message : "Er ging iets mis. Probeer het opnieuw.";
      setError(message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div style={{ maxWidth: 480 }}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="user" /> Account aanmaken
          </h1>
          <p className="page-header__subtitle">
            {state.status === "ready"
              ? `U bent uitgenodigd voor ${state.invitation.organizationName} (${state.countryName}).`
              : state.status === "loading"
                ? "Laden…"
                : "Uitnodiging"}
          </p>
        </div>
      </div>

      {state.status === "invalid" && (
        <div className="card">
          <div className="card__body">
            <div className="alert alert--warning">
              <Icon name="info" />
              <div>{state.message} Vraag de beheerder om een nieuwe link.</div>
            </div>
            <div className="form__actions">
              <Link className="btn btn--secondary" href="/login">
                Naar inloggen
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
                  E-mailadres
                </label>
                <input id="email" className="form-input" type="email" value={state.invitation.email} readOnly />
              </div>
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
                <label className="form-label" htmlFor="password">
                  Wachtwoord
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
                  <Icon name="info" /> Minimaal 12 tekens.
                </div>
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
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
