"use client";

import dynamic from "next/dynamic";

import { Icon } from "../../../../components/Icon";
import { useText } from "../../../../lib/i18n";

function Loading() {
  const t = useText();
  return (
    <div className="page-header">
      <div>
        <h1>
          <Icon name="fileText" /> {t.review.title}
        </h1>
        <p className="page-header__subtitle">{t.common.loading}</p>
      </div>
    </div>
  );
}

/**
 * The review screen is entirely client state (sessionStorage draft + in-memory
 * PDF), so it is not server-rendered. Loading text follows the Bookkeeping
 * convention: a plain "Laden…" placeholder, no spinner.
 */
const ReviewClient = dynamic(() => import("./ReviewClient"), {
  ssr: false,
  loading: () => <Loading />,
});

export default function ReviewBoot() {
  return <ReviewClient />;
}
