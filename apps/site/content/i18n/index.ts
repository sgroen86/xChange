import type { Lang } from "../langs.ts";
import { en } from "./en.ts";
import { fr } from "./fr.ts";
import { nl } from "./nl.ts";
import type { Dictionary } from "./types.ts";

export const dictionaries: Record<Lang, Dictionary> = { nl, en, fr };

export function getDictionary(lang: Lang): Dictionary {
  return dictionaries[lang];
}
