import ReviewBoot from "./ReviewBoot";

/**
 * `output: "export"` needs a concrete param list. Invoice ids are created at
 * runtime, so only a placeholder page is emitted; public/.htaccess rewrites
 * /xchange/invoices/<any-id>/review/ onto it and the client reads the real id
 * from the URL. Client-side navigation from the upload page never hits Apache.
 */
export function generateStaticParams() {
  return [{ invoiceId: "demo" }];
}

export default function ReviewPage() {
  return <ReviewBoot />;
}
