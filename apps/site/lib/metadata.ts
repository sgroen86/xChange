import type { Metadata } from "next";

import { defaultLang, langs, locales, type Lang } from "../content/langs";

/**
 * Per-page metadata: title, description, canonical URL and hreflang links to
 * the same page in the other languages. `path` is the part after the language
 * prefix ("" for home, "pricing/" …). Indexing is switched off in the root
 * layouts, not here.
 */
export function pageMetadata(
  lang: Lang,
  path: string,
  meta: { title: string; description: string },
  options: { absoluteTitle?: boolean } = {},
): Metadata {
  const languages: Record<string, string> = Object.fromEntries(langs.map((l) => [l, `/${l}/${path}`]));
  languages["x-default"] = path === "" ? "/" : `/${defaultLang}/${path}`;
  return {
    title: options.absoluteTitle ? { absolute: meta.title } : meta.title,
    description: meta.description,
    alternates: { canonical: `/${lang}/${path}`, languages },
    openGraph: {
      title: meta.title,
      description: meta.description,
      url: `/${lang}/${path}`,
      siteName: "xChange",
      locale: locales[lang].replace("-", "_"),
      type: "website",
    },
  };
}
