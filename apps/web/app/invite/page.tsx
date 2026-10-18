import { Suspense } from "react";

import InviteClient from "./InviteClient";

/** Public, like the login page: the person has no account until they finish here. */
export default function InvitePage() {
  return (
    <Suspense fallback={<p className="text-soft text-sm">Laden…</p>}>
      <InviteClient />
    </Suspense>
  );
}
