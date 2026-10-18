/**
 * Resource articles. Slugs are shared by all languages, so the language
 * switcher can move between versions of the same article.
 */
export const articleSlugs = [
  "belgium-e-invoicing-2026",
  "what-is-peppol",
  "bis-3-or-nlcius",
  "find-peppol-address",
] as const;

export type ArticleSlug = (typeof articleSlugs)[number];

/** Article text is plain text: no markup, no links inside the strings. */
export type ArticleBlock =
  | { type: "p"; text: string }
  | { type: "h2"; text: string }
  | { type: "ul"; items: string[] }
  | { type: "ol"; items: string[] }
  | { type: "note"; text: string };

export type Article = {
  title: string;
  /** One or two sentences, used as the meta description and on the index. */
  description: string;
  readingMinutes: number;
  blocks: ArticleBlock[];
};

export type Articles = Record<ArticleSlug, Article>;
