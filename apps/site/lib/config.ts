/**
 * Build-time configuration. NEXT_PUBLIC_* values are inlined by `next build`,
 * so changing them means rebuilding the site.
 */

/** The xChange web application, where "Log in" leads. */
export const appUrl = (process.env.NEXT_PUBLIC_APP_URL || "https://app.xchangeinvoice.com").replace(/\/+$/, "");

/** Public origin of this site, used for canonical and hreflang URLs. */
export const siteUrl = (process.env.NEXT_PUBLIC_SITE_URL || "https://www.xchangeinvoice.com").replace(/\/+$/, "");
