import Link from "next/link";

import { company } from "../content/company";
import type { Dictionary } from "../content/i18n/types";
import type { Lang } from "../content/langs";
import { appUrl } from "../lib/config";
import { href } from "../lib/routes";
import { LanguageSwitcher } from "./LanguageSwitcher";
import { Logo } from "./Logo";

export function SiteFooter({ lang, dict }: { lang: Lang; dict: Dictionary }) {
  const { nav, footer } = dict;
  return (
    <footer className="site-footer">
      <div className="container site-footer__grid">
        <div className="site-footer__brand">
          <Logo />
          <p>{footer.tagline}</p>
          <p className="site-footer__eu">{footer.hostedInEu}</p>
        </div>

        <nav aria-label={footer.productHeading}>
          <h2>{footer.productHeading}</h2>
          <ul>
            <li><Link href={`${href(lang, "home")}#product`}>{nav.product}</Link></li>
            <li><Link href={href(lang, "pricing")}>{nav.pricing}</Link></li>
            <li><Link href={href(lang, "resources")}>{nav.resources}</Link></li>
            <li><Link href={href(lang, "faq")}>{nav.faq}</Link></li>
          </ul>
        </nav>

        <nav aria-label={footer.companyHeading}>
          <h2>{footer.companyHeading}</h2>
          <ul>
            <li><Link href={href(lang, "contact")}>{footer.contact}</Link></li>
            <li><a href={`mailto:${company.contactEmail}`}>{company.contactEmail}</a></li>
            <li><a href={appUrl}>{nav.login}</a></li>
            <li><Link href={href(lang, "signup")}>{nav.signup}</Link></li>
          </ul>
        </nav>

        <nav aria-label={footer.legalHeading}>
          <h2>{footer.legalHeading}</h2>
          <ul>
            <li><Link href={href(lang, "privacy")}>{footer.privacy}</Link></li>
            <li><Link href={href(lang, "terms")}>{footer.terms}</Link></li>
            <li><Link href={href(lang, "trust")}>{footer.trust}</Link></li>
          </ul>
        </nav>

        <div>
          <h2>{footer.languagesHeading}</h2>
          <LanguageSwitcher lang={lang} label={footer.languagesHeading} variant="list" />
        </div>
      </div>
      <div className="container site-footer__bottom">
        <p>
          © {new Date().getFullYear()} {company.name}. {footer.rights}
        </p>
      </div>
    </footer>
  );
}
