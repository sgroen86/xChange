"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

import { languageNames, langs, type Lang } from "../content/langs";
import { switchLang } from "../lib/routes";

/**
 * Links to the same page in the other languages. Client-side only because
 * the current path is needed; every link is a plain anchor in the export.
 */
export function LanguageSwitcher({
  lang,
  label,
  variant = "compact",
  onNavigate,
}: {
  lang: Lang;
  label: string;
  variant?: "compact" | "list";
  onNavigate?: () => void;
}) {
  const pathname = usePathname() ?? `/${lang}/`;
  return (
    <nav className={`lang-switch lang-switch--${variant}`} aria-label={label}>
      <ul>
        {langs.map((l) => (
          <li key={l}>
            <Link
              href={switchLang(pathname, l)}
              hrefLang={l}
              lang={l}
              aria-current={l === lang ? "true" : undefined}
              onClick={onNavigate}
            >
              {variant === "compact" ? (
                <>
                  <span aria-hidden="true">{l.toUpperCase()}</span>
                  <span className="sr-only">{languageNames[l]}</span>
                </>
              ) : (
                languageNames[l]
              )}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
