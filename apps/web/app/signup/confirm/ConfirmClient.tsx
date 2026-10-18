"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";

import { Icon } from "../../../components/Icon";
import { useToast } from "../../../components/Toast";
import { ApiError } from "../../../lib/api";
import { text, useText } from "../../../lib/i18n";
import { confirmSignup } from "../../../lib/auth";

/**
 * Opens the confirmation link: the organisation and the account are created,
 * and the person is signed in and sent to the upload page.
 */
export default function ConfirmClient() {
  const router = useRouter();
  const showToast = useToast();
  const t = useText();
  const code = useSearchParams().get("code") ?? "";
  const [error, setError] = useState<string | null>(null);
  // A link confirms once; React's development double-run must not use it twice.
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;

    confirmSignup(code)
      .then((session) => {
        showToast(text().auth.confirm.welcome(session.user.name), "success");
        router.replace("/upload");
      })
      .catch((cause: unknown) => {
        setError(cause instanceof ApiError ? cause.message : text().auth.genericError);
      });
  }, [code, router, showToast]);

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
