"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";

import type { Dictionary } from "../content/i18n/types";
import type { Lang } from "../content/langs";
import { appUrl } from "../lib/config";
import { href } from "../lib/routes";
import { Icon } from "./Icon";
import { LanguageSwitcher } from "./LanguageSwitcher";
import { Logo } from "./Logo";

export function SiteHeader({ lang, nav }: { lang: Lang; nav: Dictionary["nav"] }) {
  const [open, setOpen] = useState(false);
  const pathname = usePathname() ?? "";
  const close = () => setOpen(false);

  const links = [
    { href: `${href(lang, "home")}#product`, label: nav.product, match: null },
    { href: href(lang, "pricing"), label: nav.pricing, match: href(lang, "pricing") },
    { href: href(lang, "resources"), label: nav.resources, match: href(lang, "resources") },
    { href: href(lang, "faq"), label: nav.faq, match: href(lang, "faq") },
  ];
  const isCurrent = (match: string | null) =>
    match !== null && `${pathname.replace(/\/?$/, "/")}`.startsWith(match);

  return (
    <header className={open ? "site-header is-open" : "site-header"}>
      <div className="container site-header__bar">
        <Link href={href(lang, "home")} className="site-header__logo" aria-label={nav.homeLabel} onClick={close}>
          <Logo />
        </Link>

        <button
          type="button"
          className="site-header__toggle"
          aria-expanded={open}
          aria-controls="site-menu"
          onClick={() => setOpen((value) => !value)}
        >
          <Icon name={open ? "x" : "menu"} />
          <span className="sr-only">{open ? nav.closeMenu : nav.openMenu}</span>
        </button>

        <div className="site-header__menu" id="site-menu">
          <nav aria-label={nav.mainNav}>
            <ul className="site-nav">
              {links.map((link) => (
                <li key={link.href}>
                  <Link
                    href={link.href}
                    aria-current={isCurrent(link.match) ? "page" : undefined}
                    onClick={close}
                  >
                    {link.label}
                  </Link>
                </li>
              ))}
            </ul>
          </nav>
          <div className="site-header__actions">
            <LanguageSwitcher lang={lang} label={nav.languageLabel} onNavigate={close} />
            <a href={appUrl} className="btn btn--ghost">
              {nav.login}
            </a>
            <Link href={href(lang, "signup")} className="btn btn--primary" onClick={close}>
              {nav.signup}
            </Link>
          </div>
        </div>
      </div>
    </header>
  );
}
