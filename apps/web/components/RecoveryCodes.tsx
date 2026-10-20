"use client";

import { useState } from "react";

import { useText } from "../lib/i18n";
import { RECOVERY_FILE_NAME, recoveryCodesText } from "../lib/mfa";
import { Icon } from "./Icon";
import { useToast } from "./Toast";

/**
 * The recovery codes, shown once: copy, download, and an "I have saved them"
 * tick before the person may continue. The server keeps only their hashes,
 * so there is no second chance to see them.
 */
export function RecoveryCodes({
  codes,
  account,
  onContinue,
}: {
  codes: string[];
  /** Who the codes belong to, written into the downloaded file. */
  account: string;
  onContinue: () => void;
}) {
  const t = useText();
  const c = t.mfa.codes;
  const showToast = useToast();
  const [saved, setSaved] = useState(false);

  const content = recoveryCodesText(c.fileHeading, account, codes);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(content);
      showToast(c.copied, "success");
    } catch {
      showToast(c.copyFailed, "warning");
    }
  };

  const download = () => {
    const url = URL.createObjectURL(new Blob([content], { type: "text/plain;charset=utf-8" }));
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = RECOVERY_FILE_NAME;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
  };

  return (
    <div>
      <h3 style={{ marginBottom: "var(--space-2)" }}>{c.title}</h3>
      <p className="text-sm text-soft" style={{ marginBottom: "var(--space-4)" }}>
        {c.intro}
      </p>

      <ul
        className="text-mono"
        aria-label={c.title}
        style={{
          listStyle: "none",
          display: "grid",
          gridTemplateColumns: "repeat(auto-fill, minmax(150px, 1fr))",
          gap: "var(--space-2) var(--space-4)",
          padding: "var(--space-4)",
          margin: "0 0 var(--space-4)",
          background: "var(--color-surface-sunken)",
          borderRadius: 10,
          userSelect: "all",
        }}
      >
        {codes.map((code) => (
          <li key={code}>{code}</li>
        ))}
      </ul>

      <div style={{ display: "flex", gap: "var(--space-2)", marginBottom: "var(--space-4)" }}>
        <button type="button" className="btn btn--secondary btn--sm" onClick={() => void copy()}>
          <Icon name="save" /> {c.copy}
        </button>
        <button type="button" className="btn btn--secondary btn--sm" onClick={download}>
          <Icon name="download" /> {c.download}
        </button>
      </div>

      <label className="checkbox" style={{ display: "flex", gap: "var(--space-2)", alignItems: "center", marginBottom: "var(--space-4)" }}>
        <input type="checkbox" checked={saved} onChange={(event) => setSaved(event.target.checked)} />
        {c.saved}
      </label>

      <button type="button" className="btn btn--primary" disabled={!saved} onClick={onContinue}>
        <Icon name="check" /> {c.continue}
      </button>
    </div>
  );
}
