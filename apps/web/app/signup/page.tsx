import { Suspense } from "react";

import SignupClient from "./SignupClient";

/** Public, like the login page: the person has no account yet. */
export default function SignupPage() {
  return (
    <Suspense fallback={<p className="text-soft text-sm">Laden…</p>}>
      <SignupClient />
    </Suspense>
  );
}
