import { Suspense } from "react";

import ConfirmClient from "./ConfirmClient";

/** The link in the confirmation mail. Public: the account is created here. */
export default function ConfirmSignupPage() {
  return (
    <Suspense fallback={<p className="text-soft text-sm">Laden…</p>}>
      <ConfirmClient />
    </Suspense>
  );
}
