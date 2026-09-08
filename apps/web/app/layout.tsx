import type { Metadata } from "next";
import { DM_Serif_Display, IBM_Plex_Mono, IBM_Plex_Sans } from "next/font/google";

import "../styles/variables.css";
import "../styles/base.css";
import "../styles/layout.css";
import "../styles/sidebar.css";
import "../styles/buttons.css";
import "../styles/forms.css";
import "../styles/tables.css";
import "../styles/modals.css";
import "../styles/xchange.css";

import { AppShell } from "../components/AppShell";
import { ToastProvider } from "../components/Toast";

/* The same three families Bookkeeping loads from Google Fonts, bound to the
   token names so --font-body / --font-display / --font-mono keep working. */
const plexSans = IBM_Plex_Sans({
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
  variable: "--font-plex-sans",
  display: "swap",
});

const plexMono = IBM_Plex_Mono({
  subsets: ["latin"],
  weight: ["400", "500"],
  variable: "--font-plex-mono",
  display: "swap",
});

const dmSerif = DM_Serif_Display({
  subsets: ["latin"],
  weight: ["400"],
  variable: "--font-dm-serif",
  display: "swap",
});

export const metadata: Metadata = {
  title: "xChange — Factuurconversie",
  description: "PDF-facturen omzetten naar gevalideerde, gestructureerde factuurdata.",
  robots: { index: false, follow: false },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="nl" className={`${plexSans.variable} ${plexMono.variable} ${dmSerif.variable}`}>
      <body>
        <ToastProvider>
          <AppShell>{children}</AppShell>
        </ToastProvider>
      </body>
    </html>
  );
}
