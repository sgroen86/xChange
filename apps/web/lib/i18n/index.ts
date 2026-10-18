/**
 * The app's texts per language. Each area keeps its own file exporting
 * `{ nl, en }` with `en: typeof nl`, so a text missing in one language is a
 * compile error. A new language is a new key in every area file plus LANGS.
 */

import { useSyncExternalStore } from "react";

import { auth } from "./auth.ts";
import { common } from "./common.ts";
import { imports } from "./imports.ts";
import { invoices } from "./invoices.ts";
import {
  currentLanguage,
  getLanguageSnapshot,
  getServerLanguageSnapshot,
  subscribeToLanguage,
  type Lang,
} from "./language.ts";
import { platform } from "./platform.ts";
import { relations } from "./relations.ts";
import { review } from "./review.ts";
import { settings } from "./settings.ts";
import { shell } from "./shell.ts";
import { upload } from "./upload.ts";

const areas = { common, shell, auth, review, upload, imports, invoices, relations, settings, platform };

type Areas = typeof areas;
export type Dictionary = { [Area in keyof Areas]: Areas[Area]["nl"] };

function build(lang: Lang): Dictionary {
  return Object.fromEntries(Object.entries(areas).map(([name, area]) => [name, area[lang]])) as Dictionary;
}

export const dictionaries: Record<Lang, Dictionary> = { nl: build("nl"), en: build("en") };

/** The current language, re-rendering the component when it is switched. */
export function useLang(): Lang {
  return useSyncExternalStore(subscribeToLanguage, getLanguageSnapshot, getServerLanguageSnapshot);
}

/** The texts in the current language, for components. */
export function useText(): Dictionary {
  return dictionaries[useLang()];
}

/** The texts in the current language, for code outside React. */
export function text(): Dictionary {
  return dictionaries[currentLanguage()];
}

export { LANGS, currentLanguage, setLanguage, type Lang } from "./language.ts";
