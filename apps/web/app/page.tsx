"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

/** Nothing lives at the root; the app starts at /upload. */
export default function HomePage() {
  const router = useRouter();

  useEffect(() => {
    router.replace("/upload");
  }, [router]);

  return (
    <div className="card">
      <div className="card__body">
        <p className="text-soft text-sm" style={{ marginBottom: "var(--space-4)" }}>
          Laden&hellip;
        </p>
        <Link className="btn btn--primary" href="/upload">
          Naar uploaden
        </Link>
      </div>
    </div>
  );
}
