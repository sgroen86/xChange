/**
 * The interface language: chosen with the switch, remembered in this browser,
 * and on a first visit taken from the browser's own preference. An external
 * store, like the session in lib/auth.ts, so components re-render on a switch
 * without a provider.
 *
 * Not kept on the user account (yet): see
 * docs/superpowers/specs/2026-10-18-engelse-schermen-design.md.
 */

export const LANGS = ["nl", "en"] as const;
export type Lang = (typeof LANGS)[number];

const STORAGE_KEY = "xchange.lang";
const listeners = new Set<() => void>();
let current: Lang | null = null;

function isLang(value: unknown): value is Lang {
  return typeof value === "string" && (LANGS as readonly string[]).includes(value);
}

/** Dutch for a browser that prefers Dutch, English for everyone else. */
export function preferredLanguage(languages: readonly string[]): Lang {
  return languages[0]?.toLowerCase().startsWith("nl") ? "nl" : "en";
}

function detect(): Lang {
  if (typeof window === "undefined") return "nl";
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    if (isLang(stored)) return stored;
  } catch {
    // Storage blocked: fall back to the browser preference.
  }
  return preferredLanguage(navigator.languages?.length ? navigator.languages : [navigator.language ?? "nl"]);
}

/** The language now, for code outside React (error messages in lib/*.ts). */
export function currentLanguage(): Lang {
  if (current === null) current = detect();
  return current;
}

export function setLanguage(lang: Lang): void {
  current = lang;
  try {
    window.localStorage.setItem(STORAGE_KEY, lang);
  } catch {
    // Not remembered; it still applies until the page is reloaded.
  }
  document.documentElement.lang = lang;
  listeners.forEach((listener) => listener());
}

export function subscribeToLanguage(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export const getLanguageSnapshot = currentLanguage;

/** The static export is prerendered in Dutch; the client switches after hydration. */
export const getServerLanguageSnapshot = (): Lang => "nl";
