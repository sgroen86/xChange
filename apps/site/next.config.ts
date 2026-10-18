import type { NextConfig } from "next";

/**
 * The marketing site is served from the root of its own domain
 * (xchangeinvoice.com) as plain static files, so:
 *
 * - output: "export"   -> `npm run build` writes everything to out/
 * - trailingSlash      -> /nl/pricing/ resolves to nl/pricing/index.html
 *                         without any server rewrite
 * - images.unoptimized -> there is no image optimiser in a static export
 *
 * No basePath: unlike apps/web, the site is not nested under a directory.
 */
const nextConfig: NextConfig = {
  output: "export",
  trailingSlash: true,
  images: { unoptimized: true },
};

export default nextConfig;
