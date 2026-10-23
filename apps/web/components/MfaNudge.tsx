"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import { getMfaStatus } from "../lib/accountMfa";
import { isApiConfigured } from "../lib/api";
import { useText } from "../lib/i18n";
import {
  MFA_STATUS_EVENT,
  dismissNudge,
  isNudgeDismissed,
  knownMfaEnabled,
  rememberMfaEnabled,
  shouldShowNudge,
} from "../lib/mfaNudge";
import { Icon } from "./Icon";

/**
 * A slim, dismissible notice for a signed-in user without two-step
 * verification. The status is asked once per session; nothing is asked or
 * shown without an API (mock mode) or after a dismissal (30 days).
 */
export function MfaNudge({ userId }: { userId: string }) {
  const t = useText();
  const [enabled, setEnabled] = useState<boolean | null>(() => knownMfaEnabled(userId));
  const [dismissed, setDismissed] = useState(() => isNudgeDismissed());

  useEffect(() => {
    if (!isApiConfigured || dismissed || enabled !== null) return;
    let cancelled = false;
    getMfaStatus()
      .then((status) => {
        rememberMfaEnabled(userId, status.enabled);
        if (!cancelled) setEnabled(status.enabled);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [userId, dismissed, enabled]);

  // Turned on (or off) in the settings: follow without asking the API again.
  useEffect(() => {
    const follow = (event: Event) => {
      const detail = (event as CustomEvent<{ userId: string; enabled: boolean }>).detail;
      if (detail?.userId === userId) setEnabled(detail.enabled);
    };
    window.addEventListener(MFA_STATUS_EVENT, follow);
    return () => window.removeEventListener(MFA_STATUS_EVENT, follow);
  }, [userId]);

  if (!isApiConfigured || !shouldShowNudge(enabled, dismissed)) return null;

  return (
    <div className="alert alert--info mfa-nudge" role="status">
      <Icon name="info" />
      <div className="mfa-nudge__text">
        {t.mfa.nudge.text}{" "}
        <Link href="/settings/#two-step" className="mfa-nudge__link">
          {t.mfa.nudge.action}
        </Link>
      </div>
      <button
        type="button"
        className="btn btn--ghost btn--sm btn--icon"
        onClick={() => {
          dismissNudge();
          setDismissed(true);
        }}
        aria-label={t.mfa.nudge.dismiss}
        title={t.mfa.nudge.dismiss}
      >
        <Icon name="x" />
      </button>
    </div>
  );
}
