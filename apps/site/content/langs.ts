/** The languages the site is published in. Dutch is the default. */
export const langs = ["nl", "en", "fr"] as const;

export type Lang = (typeof langs)[number];

export const defaultLang: Lang = "nl";

/** Each language's name in that language, for the language switcher. */
export const languageNames: Record<Lang, string> = {
  nl: "Nederlands",
  en: "English",
  fr: "Français",
};

/** BCP 47 locale used for number and date formatting. */
export const locales: Record<Lang, string> = {
  nl: "nl-NL",
  en: "en-GB",
  fr: "fr-BE",
};

export function isLang(value: string): value is Lang {
  return (langs as readonly string[]).includes(value);
}
