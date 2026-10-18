"use client";

import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { API_BASE_URL, isApiConfigured } from "../../lib/api";
import { authHeaders } from "../../lib/auth";
import { useText } from "../../lib/i18n";

interface Usage {
  used: number;
  limit: number | null;
  remaining: number | null;
}

/** The organisation's conversions. Only shown when it has a limit (a trial); otherwise there is nothing to say. */
export function UsageCard() {
  const t = useText();
  const [usage, setUsage] = useState<Usage | null>(null);

  useEffect(() => {
    if (!isApiConfigured) return;
    let cancelled = false;
    fetch(`${API_BASE_URL}/api/v1/organization/usage`, { headers: authHeaders() })
      .then((response) => (response.ok ? (response.json() as Promise<Usage>) : null))
      .then((loaded) => {
        if (!cancelled) setUsage(loaded);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  if (!usage || usage.limit === null) return null;

  const used = Math.min(usage.used, usage.limit);
  const percent = Math.round((used / Math.max(1, usage.limit)) * 100);

  return (
    <div className="card" style={{ marginTop: "var(--space-4)" }}>
      <div className="card__header">
        <h3>{t.settings.usage.title}</h3>
        <span className={`badge ${usage.remaining === 0 ? "badge--danger" : "badge--info"}`}>
          {t.settings.usage.remaining(usage.remaining)}
        </span>
      </div>
      <div className="card__body">
        <p className="text-sm">{t.settings.usage.used(used, usage.limit)}</p>
        <div
          role="progressbar"
          aria-valuemin={0}
          aria-valuemax={usage.limit}
          aria-valuenow={used}
          style={{ height: 8, borderRadius: 4, background: "var(--color-surface-sunken)", overflow: "hidden", marginTop: "var(--space-2)" }}
        >
          <div style={{ width: `${percent}%`, height: "100%", background: "var(--color-primary)" }} />
        </div>
        {usage.remaining === 0 && (
          <div className="alert alert--warning" style={{ marginTop: "var(--space-4)" }}>
            <Icon name="info" />
            <div>{t.settings.usage.exhausted}</div>
          </div>
        )}
      </div>
    </div>
  );
}
