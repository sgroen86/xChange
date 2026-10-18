"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";

import { Icon } from "../../../components/Icon";
import { useToast } from "../../../components/Toast";
import { ApiError } from "../../../lib/api";
import { confirmSignup } from "../../../lib/auth";

/**
 * Opens the confirmation link: the organisation and the account are created,
 * and the person is signed in and sent to the upload page.
 */
export default function ConfirmClient() {
  const router = useRouter();
  const showToast = useToast();
  const code = useSearchParams().get("code") ?? "";
  const [error, setError] = useState<string | null>(null);
  // A link confirms once; React's development double-run must not use it twice.
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;

    confirmSignup(code)
      .then((session) => {
        showToast(`Welkom bij xChange, ${session.user.name}.`, "success");
        router.replace("/upload");
      })
      .catch((cause: unknown) => {
        setError(cause instanceof ApiError ? cause.message : "Er ging iets mis. Probeer het opnieuw.");
      });
  }, [code, router, showToast]);

  return (
    <div style={{ maxWidth: 520 }}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="user" /> Account activeren
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
                  Opnieuw aanmelden
                </Link>
                <Link className="btn btn--ghost" href="/login">
                  Inloggen
                </Link>
              </div>
            </>
          ) : (
            <p className="text-soft text-sm">Je account wordt aangemaakt&hellip;</p>
          )}
        </div>
      </div>
    </div>
  );
}
