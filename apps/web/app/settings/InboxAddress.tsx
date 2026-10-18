"use client";

import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { API_BASE_URL, isApiConfigured } from "../../lib/api";
import { authHeaders } from "../../lib/auth";

async function inbox(method: "GET" | "POST"): Promise<string | null> {
  const response = await fetch(`${API_BASE_URL}/api/v1/organization/inbox`, { method, headers: authHeaders() });
  if (!response.ok) throw new Error(`De API gaf status ${response.status}.`);
  return ((await response.json()) as { address: string | null }).address;
}

/**
 * The organisation's invoice inbox: PDFs mailed there appear under "Te
 * beoordelen". The address is hard to guess on purpose; an admin can replace
 * it when it attracts unwanted mail, and the old one then stops working.
 */
export function InboxAddress({ canEdit }: { canEdit: boolean }) {
  const showToast = useToast();
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
    if (address && !window.confirm("Een nieuw adres maken? Het huidige adres werkt daarna niet meer.")) return;
    setBusy(true);
    try {
      setAddress(await inbox("POST"));
      showToast("Nieuw adres aangemaakt.", "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : "Aanmaken is mislukt.", "danger");
    } finally {
      setBusy(false);
    }
  };

  const copy = async () => {
    if (!address) return;
    try {
      await navigator.clipboard.writeText(address);
      showToast("Adres gekopieerd.", "success");
    } catch {
      showToast("Kopiëren lukte niet; selecteer het adres en kopieer het zelf.", "warning");
    }
  };

  return (
    <div className="card form" style={{ marginTop: "var(--space-4)" }}>
      <div className="card__header">
        <h3>E-mailadres voor facturen</h3>
      </div>
      <div className="card__body">
        <p className="text-soft text-sm">
          PDF&apos;s die naar dit adres worden gemaild, worden in de achtergrond gelezen en
          verschijnen onder <strong>Te beoordelen</strong>, met afzender en onderwerp.
        </p>
        {address === undefined ? (
          <p className="text-soft text-sm">Laden&hellip;</p>
        ) : address ? (
          <div className="form__row">
            <div className="form__group">
              <label htmlFor="inboxAddress">Adres</label>
              <input id="inboxAddress" type="text" value={address} readOnly onFocus={(e) => e.target.select()} />
            </div>
            <div className="form__group" style={{ justifyContent: "flex-end" }}>
              <button type="button" className="btn btn--secondary" onClick={() => void copy()}>
                Kopiëren
              </button>
            </div>
          </div>
        ) : (
          <p className="text-soft text-sm">Er is nog geen adres.</p>
        )}
        {canEdit && (
          <div className="form__actions">
            <button type="button" className="btn btn--secondary" onClick={() => void create()} disabled={busy}>
              <Icon name="sparkles" /> {address ? "Nieuw adres maken" : "Adres aanmaken"}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
