import type { Lang } from "../langs.ts";
import { articlesEn } from "./en.ts";
import { articlesFr } from "./fr.ts";
import { articlesNl } from "./nl.ts";
import type { Articles } from "./types.ts";

export const articles: Record<Lang, Articles> = { nl: articlesNl, en: articlesEn, fr: articlesFr };
