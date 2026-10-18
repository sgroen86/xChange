/**
 * Session handling for the browser.
 *
 * The token is an opaque server-side session id, not a self-contained token, so
 * the browser only ever holds a reference. It lives in sessionStorage rather
 * than localStorage: closing the tab ends the session, which matches how the
 * Bookkeeping app's PHP session cookie behaves.
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { text } from "./i18n";
import { currentLanguage } from "./i18n/language";

const TOKEN_KEY = "xchange.session.token";
const USER_KEY = "xchange.session.user";

export type UserRole = "Admin" | "User" | "ReadOnly";

export interface SessionUser {
  id: string;
  organizationId: string;
  email: string;
  name: string;
  role: UserRole;
  /** Manages organisations; grants no access to other organisations' data. */
  isPlatformAdmin?: boolean;
}

export interface CountryOption {
  code: string;
  name: string;
}

export interface InvitationDetails {
  organizationName: string;
  countryCode: string;
  email: string;
  name: string;
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

/** Just the language, for the public calls made before there is a session. */
function languageHeader(): Record<string, string> {
  return { "Accept-Language": currentLanguage() };
}

/** Headers for a call: the language, and the session when signed in. */
export function authHeaders(): Record<string, string> {
  const token = getToken();
  const language = languageHeader();
  return token ? { ...language, Authorization: `Bearer ${token}` } : language;
}

async function post<T>(path: string, body: unknown): Promise<T> {
  if (!isApiConfigured) {
    throw new ApiError(text().common.noApi, 0);
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...authHeaders() },
      body: JSON.stringify(body),
    });
  } catch {
    throw new ApiError(text().common.apiUnreachable, 0);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const payload = await response.json().catch(() => null);

  if (!response.ok) {
    const detail =
      (payload as { detail?: string; title?: string } | null)?.detail ??
      (payload as { title?: string } | null)?.title ??
      text().common.apiStatus(response.status);
    throw new ApiError(detail, response.status);
  }

  return payload as T;
}

/** Whether the very first account still needs creating, as in Bookkeeping's setup.php. */
export async function isSetupRequired(): Promise<boolean> {
  if (!isApiConfigured) return false;

  try {
    const response = await fetch(`${API_BASE_URL}/api/v1/auth/status`, { headers: languageHeader() });
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
  countryCode: string,
): Promise<Session> {
  const session = await post<Session>("/api/v1/auth/setup", {
    email,
    password,
    name,
    organizationName,
    countryCode,
  });
  store(session);
  return session;
}

/** The countries an organisation can be in. Public: the setup screen needs it before any login. */
export async function listCountries(): Promise<CountryOption[]> {
  if (!isApiConfigured) return [];
  const response = await fetch(`${API_BASE_URL}/api/v1/countries`, { headers: languageHeader() }).catch(() => null);
  return response?.ok ? ((await response.json()) as CountryOption[]) : [];
}

/** What an invitation link shows before the account exists. */
export async function getInvitation(code: string): Promise<InvitationDetails> {
  if (!isApiConfigured) throw new ApiError(text().common.noApi, 0);

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/v1/invitations/${encodeURIComponent(code)}`, {
      headers: languageHeader(),
    });
  } catch {
    throw new ApiError(text().common.apiUnreachable, 0);
  }

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    throw new ApiError(
      (payload as { detail?: string } | null)?.detail ?? text().auth.invite.invalid,
      response.status,
    );
  }
  return payload as InvitationDetails;
}

/** Turns an invitation into an account and signs in with it. */
export async function acceptInvitation(code: string, name: string, password: string): Promise<Session> {
  const session = await post<Session>(`/api/v1/invitations/${encodeURIComponent(code)}/accept`, { name, password });
  store(session);
  return session;
}

/** Whether anyone may sign up on their own (the platform administrator opens it at launch). */
export async function signupStatus(): Promise<{ open: boolean; trialConversions: number }> {
  if (!isApiConfigured) return { open: false, trialConversions: 0 };
  const response = await fetch(`${API_BASE_URL}/api/v1/signup/status`, { headers: languageHeader() }).catch(() => null);
  return response?.ok ? ((await response.json()) as { open: boolean; trialConversions: number }) : { open: false, trialConversions: 0 };
}

/** Sends the confirmation mail. The answer is the same whether or not the address already has an account. */
export async function startSignup(input: {
  name: string;
  email: string;
  password: string;
  organizationName: string;
  countryCode: string;
}): Promise<void> {
  await post<void>("/api/v1/signup", input);
}

/** The link in the confirmation mail: creates the organisation and signs in. */
export async function confirmSignup(code: string): Promise<Session> {
  const session = await post<Session>("/api/v1/signup/confirm", { code });
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
