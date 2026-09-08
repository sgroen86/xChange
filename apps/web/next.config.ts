import type { NextConfig } from "next";

/**
 * greenitsolutions.net is Apache + PHP shared hosting with no Node runtime, so
 * the app ships as a static export dropped into /xchange/ on that host.
 *
 * - basePath/assetPrefix -> every asset and link is emitted under /xchange
 * - trailingSlash        -> /xchange/upload/ resolves to upload/index.html,
 *                           which Apache serves without any rewrite
 * - images.unoptimized   -> there is no image optimiser in a static export
 */
const nextConfig: NextConfig = {
  output: "export",
  basePath: "/xchange",
  trailingSlash: true,
  images: { unoptimized: true },
};

export default nextConfig;
