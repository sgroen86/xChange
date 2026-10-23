"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";

import { Icon } from "../../../components/Icon";
import { MfaSignIn } from "../../../components/MfaSignIn";
import { useToast } from "../../../components/Toast";
import { ApiError } from "../../../lib/api";
import { text, useText } from "../../../lib/i18n";
import { confirmSignup, type Session } from "../../../lib/auth";
import { rememberedPlan } from "../../../lib/signupPlan";

/**
 * Where a new admin goes once signed in: to the upload page, or - with a plan
 * chosen on the website and remembered in this browser - to the settings page,
 * which starts Checkout for it.
 */
function landing(): string {
  const plan = rememberedPlan();
  return plan ? `/settings/?checkout=${plan}` : "/upload";
}

/**
 * Opens the confirmation link: the organisation and the account are created.
 * The new user is its Admin, for whom two-step verification is mandatory, so
 * they set it up here before they are signed in and sent on (see landing()).
 */
export default function ConfirmClient() {
  const router = useRouter();
  const showToast = useToast();
  const t = useText();
  const code = useSearchParams().get("code") ?? "";
  const [error, setError] = useState<string | null>(null);
  const [mfaStep, setMfaStep] = useState<{ kind: "mfa" | "mfaSetup"; challenge: string } | null>(null);
  // A link confirms once; React's development double-run must not use it twice.
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;

    confirmSignup(code)
      .then((result) => {
        if (result.kind === "session") {
          showToast(text().auth.confirm.welcome(result.session.user.name), "success");
          router.replace(landing());
        } else {
          setMfaStep(result);
        }
      })
      .catch((cause: unknown) => {
        setError(cause instanceof ApiError ? cause.message : text().auth.genericError);
      });
  }, [code, router, showToast]);

  if (mfaStep) {
    return (
      <MfaSignIn
        step={mfaStep}
        account=""
        onSignedIn={(session: Session) => {
          showToast(text().auth.confirm.welcome(session.user.name), "success");
          router.replace(landing());
        }}
        // The account exists now; what remains is an ordinary login.
        onRestart={() => router.replace("/login")}
      />
    );
  }

  return (
    <div style={{ maxWidth: 520 }}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="user" /> {t.auth.confirm.title}
          </h1>
        </div>
      </div>
      <div className="card">
        <div className="card__body">
          {error ? (
            <>
              <div className="alert alert--warning">
                <Icon name="info" />
                <div>{error}</div>
              </div>
              <div className="form__actions">
                <Link className="btn btn--secondary" href="/signup">
                  {t.auth.confirm.signupAgain}
                </Link>
                <Link className="btn btn--ghost" href="/login">
                  {t.auth.login}
                </Link>
              </div>
            </>
          ) : (
            <p className="text-soft text-sm">{t.auth.confirm.creating}</p>
          )}
        </div>
      </div>
    </div>
  );
}
