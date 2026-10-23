import type { NextConfig } from "next";

/**
 * The app ships as a static export to S3 behind CloudFront
 * (app.xchangeinvoice.com, infra/aws/app.yml); there is no Node runtime.
 *
 * - basePath/assetPrefix -> every asset and link is emitted under the base path
 *                           (empty: the app sits at the root of its domain)
 * - trailingSlash        -> /upload/ resolves to upload/index.html, which the
 *                           CloudFront function serves as the directory index
 * - images.unoptimized   -> there is no image optimiser in a static export
 *
 * The base path is an input (XCHANGE_BASE_PATH) in case the app is ever served
 * under a sub-path again.
 */
const basePath = process.env.XCHANGE_BASE_PATH ?? "";

const nextConfig: NextConfig = {
  output: "export",
  basePath,
  trailingSlash: true,
  images: { unoptimized: true },
};

export default nextConfig;
