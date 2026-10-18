import { DM_Serif_Display, IBM_Plex_Mono, IBM_Plex_Sans } from "next/font/google";

/* The same three families the app loads (apps/web/app/layout.tsx), bound to
   the token names so --font-body / --font-display / --font-mono keep working.
   Shared by both root layouts: app/(root) and app/[lang]. */
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

export const fontClassName = `${plexSans.variable} ${plexMono.variable} ${dmSerif.variable}`;
