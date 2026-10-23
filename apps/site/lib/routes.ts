import type { Lang } from "../content/langs.ts";
import type { PlanId } from "../content/pricing.ts";
import { appUrl } from "./config.ts";

/** Paths of every page under a language prefix, without the prefix. */
export const pages = {
  home: "",
  pricing: "pricing/",
  resources: "resources/",
  faq: "faq/",
  contact: "contact/",
  signup: "signup/",
  login: "login/",
  privacy: "privacy/",
  terms: "terms/",
  trust: "trust/",
} as const;

export type PageKey = keyof typeof pages;

/** "/nl/pricing/" — trailing slash matches the static export layout. */
export function href(lang: Lang, page: PageKey | string): string {
  const path = page in pages ? pages[page as PageKey] : page;
  return `/${lang}/${path}`;
}

export function articleHref(lang: Lang, slug: string): string {
  return `/${lang}/resources/${slug}/`;
}

/** Swaps the language prefix of a pathname, keeping the rest of the path. */
export function switchLang(pathname: string, lang: Lang): string {
  const rest = pathname.replace(/^\/(nl|en|fr)(?=\/|$)/, "").replace(/^\/+/, "");
  const path = rest === "" || rest.endsWith("/") ? rest : `${rest}/`;
  return `/${lang}/${path}`;
}

/** What the app's sign-up accepts as ?plan=: a monthly plan, or the credit pack. */
export type SignupChoice = PlanId | "credits";

/** The app speaks Dutch and English; French visitors get English until it speaks French. */
export function appLang(lang: Lang): "nl" | "en" {
  return lang === "nl" ? "nl" : "en";
}

/**
 * Sign-up in the app, in the visitor's language, optionally with a plan the
 * app takes them to Checkout for right after confirming. Without a plan they
 * start on the free trial.
 */
export function appSignupHref(lang: Lang, plan?: SignupChoice): string {
  const query = new URLSearchParams();
  if (plan) query.set("plan", plan);
  query.set("lang", appLang(lang));
  return `${appUrl}/signup/?${query.toString()}`;
}
