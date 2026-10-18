import { notFound } from "next/navigation";

import { isLang, langs, type Lang } from "../content/langs";

export type LangParams = { params: Promise<{ lang: string }> };

/** Static params for every page under [lang]. */
export function langParams() {
  return langs.map((lang) => ({ lang }));
}

/** Resolves the [lang] segment, 404ing on anything that is not a site language. */
export async function resolveLang(params: Promise<{ lang: string }>): Promise<Lang> {
  const { lang } = await params;
  if (!isLang(lang)) notFound();
  return lang;
}
