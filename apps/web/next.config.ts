import type { NextConfig } from "next";

/**
 * greenitsolutions.net is Apache + PHP shared hosting with no Node runtime, so
 * the app ships as a static export dropped into a directory on that host.
 *
 * - basePath/assetPrefix -> every asset and link is emitted under the base path
 * - trailingSlash        -> /xchange/upload/ resolves to upload/index.html,
 *                           which Apache serves without any rewrite
 * - images.unoptimized   -> there is no image optimiser in a static export
 *
 * The base path is baked in at build time, so it is an input rather than a
 * constant: production builds with the default, and a future staging lane at
 * https://www.greenitsolutions.net/DEV/xchange/ only needs
 * XCHANGE_BASE_PATH=/DEV/xchange.
 */
const basePath = process.env.XCHANGE_BASE_PATH ?? "/xchange";

const nextConfig: NextConfig = {
  output: "export",
  basePath,
  trailingSlash: true,
  images: { unoptimized: true },
};

export default nextConfig;
