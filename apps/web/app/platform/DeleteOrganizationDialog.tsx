"use client";

import { useEffect, useId, useRef, useState } from "react";

import { Icon } from "../../components/Icon";
import { useText } from "../../lib/i18n";
import { deleteOrganization, type PlatformOrganization } from "../../lib/platform";

/**
 * Confirms deleting an organisation, which cannot be undone: says what goes,
 * and only enables the button once the organisation's name is typed exactly
 * (the API checks it again). A refusal - the caller's own organisation, a
 * platform administrator in it, a running subscription, a wrong name - is
 * shown here, in the API's words, and the dialog stays open.
 */
export function DeleteOrganizationDialog({
  organization,
  onClose,
  onDeleted,
}: {
  organization: PlatformOrganization;
  onClose: () => void;
  onDeleted: () => void;
}) {
  const t = useText();
  const r = t.platform.remove;
  const titleId = useId();
  const inputId = useId();
  const input = useRef<HTMLInputElement>(null);
  const [typed, setTyped] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const matches = typed.trim() === organization.name.trim();

  useEffect(() => {
    input.current?.focus();
  }, []);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !busy) onClose();
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [busy, onClose]);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!matches || busy) return;

    setBusy(true);
    setError(null);
    try {
      await deleteOrganization(organization.organizationId, typed);
      onDeleted();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : r.failed);
      setBusy(false);
    }
  };

  return (
    <div
      className="modal-backdrop"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !busy) onClose();
      }}
    >
      <form className="modal form" role="dialog" aria-modal="true" aria-labelledby={titleId} onSubmit={submit}>
        <div className="modal__header">
          <h3 id={titleId}>{r.title(organization.name)}</h3>
          <button type="button" className="modal__close" onClick={onClose} disabled={busy} aria-label={t.common.close}>
            <Icon name="x" />
          </button>
        </div>

        <div className="modal__body">
          <div className="alert alert--danger" style={{ marginBottom: "var(--space-4)" }}>
            <Icon name="info" />
            <div>
              <div className="alert__title">{r.irreversible}</div>
              <ul className="alert__list">
                {r.what.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </div>
          </div>

          <p className="text-soft text-sm" style={{ marginBottom: "var(--space-4)" }}>
            {r.stripe}
          </p>

          <div className="form__group">
            <label htmlFor={inputId}>{r.confirmLabel(organization.name)}</label>
            <input
              id={inputId}
              ref={input}
              type="text"
              value={typed}
              onChange={(event) => setTyped(event.target.value)}
              autoComplete="off"
              spellCheck={false}
              disabled={busy}
            />
          </div>

          {error && (
            <div className="alert alert--danger" role="alert">
              <Icon name="info" />
              <div>{error}</div>
            </div>
          )}
        </div>

        <div className="modal__footer">
          <button type="button" className="btn btn--secondary" onClick={onClose} disabled={busy}>
            {t.common.cancel}
          </button>
          <button type="submit" className="btn btn--danger" disabled={!matches || busy}>
            <Icon name="trash" /> {busy ? t.common.busy : r.confirm}
          </button>
        </div>
      </form>
    </div>
  );
}
