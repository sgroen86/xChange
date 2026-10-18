"use client";

import { useEffect } from "react";

import { LANGS, setLanguage, useLang, useText } from "../lib/i18n";

/** NL | EN, in the header and on the pages before login. */
export function LanguageSwitch() {
  const lang = useLang();
  const t = useText();

  // The page is prerendered as Dutch; this keeps <html lang> right after hydration.
  useEffect(() => {
    document.documentElement.lang = lang;
  }, [lang]);

  return (
    <div className="lang-switch" role="group" aria-label={t.common.language}>
      {LANGS.map((option) => (
        <button
          key={option}
          type="button"
          className={`lang-switch__option ${option === lang ? "lang-switch__option--active" : ""}`.trim()}
          aria-pressed={option === lang}
          onClick={() => setLanguage(option)}
        >
          {option.toUpperCase()}
        </button>
      ))}
    </div>
  );
}
