"use client";

import dynamic from "next/dynamic";

import { Icon } from "../../../../components/Icon";

/**
 * The review screen is entirely client state (sessionStorage draft + in-memory
 * PDF), so it is not server-rendered. Loading text follows the Bookkeeping
 * convention: a plain "Laden…" placeholder, no spinner.
 */
const ReviewClient = dynamic(() => import("./ReviewClient"), {
  ssr: false,
  loading: () => (
    <div className="page-header">
      <div>
        <h1>
          <Icon name="fileText" /> Factuur beoordelen
        </h1>
        <p className="page-header__subtitle">Laden&hellip;</p>
      </div>
    </div>
  ),
});

export default function ReviewBoot() {
  return <ReviewClient />;
}
