/**
 * The gentle nudge towards two-step verification, which is optional for
 * customers (owner's decision, 2026-10-23): a slim notice in the app for a
 * signed-in user without it, dismissed per browser for 30 days.
 *
 * Two small stores, both behind try/catch because storage can be blocked:
 * - the dismissal, in localStorage (per browser, survives the session);
 * - whether this user has MFA on, in sessionStorage, so GET /account/mfa is
 *   asked once per session rather than on every page. Keyed by user id, so
 *   another login in the same tab does not inherit it.
 *
 * Pure apart from the storage handed in, so it is tested without a browser.
 */

type NudgeStorage = Pick<Storage, "getItem" | "setItem" | "removeItem">;

const DISMISSED_KEY = "xchange.mfaNudgeDismissedAt";
const STATUS_KEY = "xchange.mfaEnabled";

/** How long a dismissal lasts. */
export const NUDGE_DISMISS_MS = 30 * 24 * 60 * 60 * 1000;

/** Fired on window when the status is learnt or changed (from the settings), so the notice follows. */
export const MFA_STATUS_EVENT = "xchange:mfa-status";

function localStore(): NudgeStorage | null {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    return null;
  }
}

function sessionStore(): NudgeStorage | null {
  try {
    return typeof window === "undefined" ? null : window.sessionStorage;
  } catch {
    return null;
  }
}

/** Whether the notice was dismissed in this browser less than 30 days ago. */
export function isNudgeDismissed(storage = localStore(), now = Date.now()): boolean {
  try {
    const raw = storage?.getItem(DISMISSED_KEY);
    if (!raw) return false;
    const at = Number(raw);
    return Number.isFinite(at) && now - at >= 0 && now - at < NUDGE_DISMISS_MS;
  } catch {
    return false;
  }
}

export function dismissNudge(storage = localStore(), now = Date.now()): void {
  try {
    storage?.setItem(DISMISSED_KEY, String(now));
  } catch {
    // Storage blocked: the notice is hidden for this page view only.
  }
}

/** What this session already knows about the user's MFA: on, off, or null when not asked yet. */
export function knownMfaEnabled(userId: string, storage = sessionStore()): boolean | null {
  try {
    const raw = storage?.getItem(STATUS_KEY);
    if (!raw) return null;
    const stored = JSON.parse(raw) as { userId?: unknown; enabled?: unknown };
    return stored.userId === userId && typeof stored.enabled === "boolean" ? stored.enabled : null;
  } catch {
    return null;
  }
}

/** Records the status for this session and tells the notice. */
export function rememberMfaEnabled(userId: string, enabled: boolean, storage = sessionStore()): void {
  try {
    storage?.setItem(STATUS_KEY, JSON.stringify({ userId, enabled }));
  } catch {
    // Storage blocked: the status is asked again on the next page load.
  }
  if (typeof window !== "undefined") {
    window.dispatchEvent(new CustomEvent(MFA_STATUS_EVENT, { detail: { userId, enabled } }));
  }
}

/** Shown only when the status is known to be off and the notice was not dismissed. */
export function shouldShowNudge(enabled: boolean | null, dismissed: boolean): boolean {
  return enabled === false && !dismissed;
}
