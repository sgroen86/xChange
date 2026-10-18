"use client";

import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { fieldLabel, forgetLearned, listLearned, valueLabel, type LearnedCorrection } from "../../lib/learning";

/**
 * What xChange learned from the organisation's corrections, with a way to
 * make it forget one. Corrections marked "toegepast" are put back by xChange
 * itself; hints only help the model read the next invoice.
 */
export function LearnedCorrections({ canForget }: { canForget: boolean }) {
  const showToast = useToast();
  const [items, setItems] = useState<LearnedCorrection[] | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    listLearned()
      .then((loaded) => {
        if (!cancelled) setItems(loaded);
      })
      .catch(() => {
        if (!cancelled) setItems([]);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const forget = async (id: string) => {
    setBusy(id);
    try {
      await forgetLearned(id);
      setItems((current) => current?.filter((item) => item.id !== id) ?? null);
      showToast("Correctie vergeten.", "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : "Vergeten is mislukt.", "danger");
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="card" style={{ marginTop: "var(--space-4)" }}>
      <div className="card__header">
        <h3>Geleerd van correcties</h3>
        {items !== null && <span className="badge badge--neutral">{items.length}</span>}
      </div>
      <div className="card__body">
        <p className="text-soft text-sm">
          Wat u bij het beoordelen verbetert, onthoudt xChange voor de volgende factuur. Uw eigen
          gegevens en de btw per afnemer worden weer zo ingevuld; de rest helpt het model om beter
          te lezen.
        </p>
      </div>
      {items !== null && items.length > 0 && (
        <div className="card__body card__body--flush">
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Veld</th>
                  <th>Gelezen → verbeterd</th>
                  <th>Voor</th>
                  <th>Gebruik</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr key={item.id}>
                    <td>
                      {fieldLabel(item.field)}
                      {item.context && <div className="text-xs text-muted">{item.context}</div>}
                    </td>
                    <td>
                      {valueLabel(item.field, item.from)} → {valueLabel(item.field, item.to)}
                      {item.count > 1 && <div className="text-xs text-muted">{item.count}× verbeterd</div>}
                    </td>
                    <td>{item.scope.startsWith("buyer:") ? `Afnemer ${item.scope.slice("buyer:".length)}` : "Alle facturen"}</td>
                    <td>
                      <span className={`badge ${item.use === "apply" ? "badge--success" : "badge--info"}`}>
                        {item.use === "apply" ? "Toegepast" : "Hint"}
                      </span>
                    </td>
                    <td>
                      {canForget && (
                        <button
                          type="button"
                          className="btn btn--ghost btn--sm"
                          disabled={busy === item.id}
                          onClick={() => void forget(item.id)}
                        >
                          <Icon name="trash" /> Vergeten
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
