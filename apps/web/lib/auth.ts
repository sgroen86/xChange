/**
 * Session handling for the browser.
 *
 * The token is an opaque server-side session id, not a self-contained token, so
 * the browser only ever holds a reference. It lives in sessionStorage rather
 * than localStorage: closing the tab ends the session, which matches how the
 * Bookkeeping app's PHP session cookie behaves.
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";

const TOKEN_KEY = "xchange.session.token";
const USER_KEY = "xchange.session.user";

export type UserRole = "Admin" | "User" | "ReadOnly";

export interface SessionUser {
  id: string;
  organizationId: string;
  email: string;
  name: string;
  role: UserRole;
}

export interface Session {
  token: string;
  expiresAt: string;
  user: SessionUser;
}

export function getToken(): string | null {
  try {
    return sessionStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

export function getUser(): SessionUser | null {
  try {
    const raw = sessionStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as SessionUser) : null;
  } catch {
    return null;
  }
}

/* --- React integration -------------------------------------------------- --
 * The session lives in sessionStorage, which is an external store rather than
 * React state. useSyncExternalStore is the supported way to read one, and it
 * needs a *stable* snapshot: returning a freshly parsed object on every call
 * would loop forever, so the parse is cached and invalidated on write. */

let snapshot: SessionUser | null | undefined;
const listeners = new Set<() => void>();

function invalidate(): void {
  snapshot = undefined;
  listeners.forEach((listener) => listener());
}

export function subscribeToUser(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function getUserSnapshot(): SessionUser | null {
  if (snapshot === undefined) {
    snapshot = getUser();
  }
  return snapshot;
}

/** No session exists while prerendering, so the server snapshot is always null. */
export function getServerUserSnapshot(): SessionUser | null {
  return null;
}

function store(session: Session): void {
  try {
    sessionStorage.setItem(TOKEN_KEY, session.token);
    sessionStorage.setItem(USER_KEY, JSON.stringify(session.user));
  } catch {
    // Storage blocked: the page still works for this navigation.
  } finally {
    invalidate();
  }
}

export function clearSession(): void {
  try {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
  } catch {
    // Nothing to clear.
  } finally {
    invalidate();
  }
}

/** Header for an authenticated call, or nothing when signed out. */
export function authHeaders(): Record<string, string> {
  const token = getToken();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function post<T>(path: string, body: unknown): Promise<T> {
  if (!isApiConfigured) {
    throw new ApiError("Er is geen API geconfigureerd.", 0);
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...authHeaders() },
      body: JSON.stringify(body),
    });
  } catch {
    throw new ApiError("De API is niet bereikbaar.", 0);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const payload = await response.json().catch(() => null);

  if (!response.ok) {
    const detail =
      (payload as { detail?: string; title?: string } | null)?.detail ??
      (payload as { title?: string } | null)?.title ??
      `De API gaf status ${response.status}.`;
    throw new ApiError(detail, response.status);
  }

  return payload as T;
}

/** Whether the very first account still needs creating, as in Bookkeeping's setup.php. */
export async function isSetupRequired(): Promise<boolean> {
  if (!isApiConfigured) return false;

  try {
    const response = await fetch(`${API_BASE_URL}/api/v1/auth/status`);
    if (!response.ok) return false;
    const body = (await response.json()) as { setupRequired: boolean };
    return body.setupRequired;
  } catch {
    return false;
  }
}

export async function login(email: string, password: string): Promise<Session> {
  const session = await post<Session>("/api/v1/auth/login", { email, password });
  store(session);
  return session;
}

export async function setupFirstUser(
  email: string,
  password: string,
  name: string,
  organizationName: string,
): Promise<Session> {
  const session = await post<Session>("/api/v1/auth/setup", {
    email,
    password,
    name,
    organizationName,
  });
  store(session);
  return session;
}

export async function logout(): Promise<void> {
  try {
    await post<void>("/api/v1/auth/logout", {});
  } catch {
    // Even if the call fails, drop the local session: the user asked to leave.
  } finally {
    clearSession();
  }
}
