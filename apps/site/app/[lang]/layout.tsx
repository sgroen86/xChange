import type { Metadata } from "next";
import type { ReactNode } from "react";

import "../../styles/variables.css";
import "../../styles/site.css";

import { SiteFooter } from "../../components/SiteFooter";
import { SiteHeader } from "../../components/SiteHeader";
import { getDictionary } from "../../content/i18n";
import { siteUrl } from "../../lib/config";
import { fontClassName } from "../../lib/fonts";
import { langParams, resolveLang } from "../../lib/lang";

/* One root layout per language, so <html lang> is right on every page.
   The language chooser at "/" has its own root layout in app/(root). */

export const dynamicParams = false;

export function generateStaticParams() {
  return langParams();
}

export async function generateMetadata({ params }: { params: Promise<{ lang: string }> }): Promise<Metadata> {
  const dict = getDictionary(await resolveLang(params));
  return {
    metadataBase: new URL(siteUrl),
    title: { template: `%s · ${dict.meta.siteName}`, default: dict.meta.siteName },
    description: dict.meta.description,
    // In development: keep every page out of search engines (see also public/robots.txt).
    robots: { index: false, follow: false },
  };
}

export default async function LangLayout({
  children,
  params,
}: {
  children: ReactNode;
  params: Promise<{ lang: string }>;
}) {
  const lang = await resolveLang(params);
  const dict = getDictionary(lang);
  return (
    <html lang={lang} className={fontClassName}>
      <body>
        <a href="#main" className="skip-link">
          {dict.nav.skipToContent}
        </a>
        <SiteHeader lang={lang} nav={dict.nav} />
        <main id="main" tabIndex={-1}>
          {children}
        </main>
        <SiteFooter lang={lang} dict={dict} />
      </body>
    </html>
  );
}
