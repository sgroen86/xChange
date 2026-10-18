"use client";

import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { API_BASE_URL, isApiConfigured } from "../../lib/api";
import { authHeaders } from "../../lib/auth";
import { text, useText } from "../../lib/i18n";

async function inbox(method: "GET" | "POST"): Promise<string | null> {
  const response = await fetch(`${API_BASE_URL}/api/v1/organization/inbox`, { method, headers: authHeaders() });
  if (!response.ok) throw new Error(text().common.apiStatus(response.status));
  return ((await response.json()) as { address: string | null }).address;
}

/**
 * The organisation's invoice inbox: PDFs mailed there appear under "Te
 * beoordelen". The address is hard to guess on purpose; an admin can replace
 * it when it attracts unwanted mail, and the old one then stops working.
 */
export function InboxAddress({ canEdit }: { canEdit: boolean }) {
  const showToast = useToast();
  const t = useText();
  const [address, setAddress] = useState<string | null | undefined>(undefined);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    inbox("GET")
      .then((loaded) => {
        if (!cancelled) setAddress(loaded);
      })
      .catch(() => {
        if (!cancelled) setAddress(null);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  if (!isApiConfigured) return null;

  const create = async () => {
    if (address && !window.confirm(t.settings.inbox.replaceConfirm)) return;
    setBusy(true);
    try {
      setAddress(await inbox("POST"));
      showToast(t.settings.inbox.created, "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : t.settings.inbox.createFailed, "danger");
    } finally {
      setBusy(false);
    }
  };

  const copy = async () => {
    if (!address) return;
    try {
      await navigator.clipboard.writeText(address);
      showToast(t.settings.inbox.copied, "success");
    } catch {
      showToast(t.settings.inbox.copyFailed, "warning");
    }
  };

  return (
    <div className="card form" style={{ marginTop: "var(--space-4)" }}>
      <div className="card__header">
        <h3>{t.settings.inbox.title}</h3>
      </div>
      <div className="card__body">
        <p className="text-soft text-sm">
          {t.settings.inbox.introBefore}
          <strong>{t.settings.inbox.introPlace}</strong>
          {t.settings.inbox.introAfter}
        </p>
        {address === undefined ? (
          <p className="text-soft text-sm">{t.common.loading}</p>
        ) : address ? (
          <div className="form__row">
            <div className="form__group">
              <label htmlFor="inboxAddress">{t.settings.inbox.address}</label>
              <input id="inboxAddress" type="text" value={address} readOnly onFocus={(e) => e.target.select()} />
            </div>
            <div className="form__group" style={{ justifyContent: "flex-end" }}>
              <button type="button" className="btn btn--secondary" onClick={() => void copy()}>
                {t.settings.inbox.copy}
              </button>
            </div>
          </div>
        ) : (
          <p className="text-soft text-sm">{t.settings.inbox.none}</p>
        )}
        {canEdit && (
          <div className="form__actions">
            <button type="button" className="btn btn--secondary" onClick={() => void create()} disabled={busy}>
              <Icon name="sparkles" /> {address ? t.settings.inbox.replace : t.settings.inbox.create}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
