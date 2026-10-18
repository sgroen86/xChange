import type { Metadata } from "next";
import type { ReactNode } from "react";

import "../../styles/variables.css";
import "../../styles/site.css";

import { siteUrl } from "../../lib/config";
import { fontClassName } from "../../lib/fonts";

/* Root layout for "/" only: the language chooser. Every other page lives
   under app/[lang], which has its own root layout with the right <html lang>. */

export const metadata: Metadata = {
  metadataBase: new URL(siteUrl),
  title: "xChange",
  description: "xChange — van PDF-factuur naar gevalideerde e-factuur.",
  // In development: keep every page out of search engines (see also public/robots.txt).
  robots: { index: false, follow: false },
  alternates: {
    canonical: "/",
    languages: { nl: "/nl/", en: "/en/", fr: "/fr/", "x-default": "/" },
  },
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="nl" className={fontClassName}>
      <body>{children}</body>
    </html>
  );
}
