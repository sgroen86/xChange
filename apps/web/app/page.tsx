"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

import { useText } from "../lib/i18n";

/** Nothing lives at the root; the app starts at /upload. */
export default function HomePage() {
  const router = useRouter();
  const t = useText();

  useEffect(() => {
    router.replace("/upload");
  }, [router]);

  return (
    <div className="card">
      <div className="card__body">
        <p className="text-soft text-sm" style={{ marginBottom: "var(--space-4)" }}>
          {t.common.loading}
        </p>
        <Link className="btn btn--primary" href="/upload">
          {t.shell.toUpload}
        </Link>
      </div>
    </div>
  );
}
