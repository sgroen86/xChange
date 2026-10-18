import type { Lang } from "../content/langs.ts";

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
