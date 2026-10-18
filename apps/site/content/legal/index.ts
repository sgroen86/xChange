import type { Lang } from "../langs.ts";
import { legalEn } from "./en.ts";
import { legalFr } from "./fr.ts";
import { legalNl } from "./nl.ts";
import type { LegalContent } from "./types.ts";

export const legal: Record<Lang, LegalContent> = { nl: legalNl, en: legalEn, fr: legalFr };
