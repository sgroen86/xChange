import ReviewBoot from "./ReviewBoot";

/**
 * `output: "export"` needs a concrete param list. Invoice ids are created at
 * runtime, so only a placeholder page is emitted; the CloudFront request
 * function (infra/aws/app.yml) serves /invoices/<any-id>/review/ from it and the
 * client reads the real id from the URL. Client-side navigation never hits it.
 */
export function generateStaticParams() {
  return [{ invoiceId: "demo" }];
}

export default function ReviewPage() {
  return <ReviewBoot />;
}
