"use client";

import type { CanonicalAllowanceCharge } from "../../../../lib/canonical";
import { formatMoney } from "../../../../lib/money";
import type { CalculatedTotals } from "../../../../lib/totals";

/** Categories whose VAT breakdown must carry an exemption reason (BR-E-10 and friends). */
const NEEDS_REASON = new Set(["E", "AE", "G", "O"]);

interface Props {
  totals: CalculatedTotals;
  currency: string | null;
  documentAdjustments: CanonicalAllowanceCharge[];
  reasonFor: (category: string, rate: string) => string;
  onReason: (category: string, rate: string, reason: string) => void;
  invalid: ReadonlySet<string>;
  readOnly: boolean;
}

/**
 * Calculated, not editable: the same figures the exported file will carry.
 * Only the exemption reason per VAT category is entered here.
 */
export function TotalsCard({ totals, currency, documentAdjustments, reasonFor, onReason, invalid, readOnly }: Props) {
  return (
    <div className="card">
      <div className="card__header">
        <h3>Totalen</h3>
        <span className="badge badge--neutral">Berekend</span>
      </div>
      <div className="card__body">
        <div className="totals">
          <div className="totals__row">
            <span>Totaal regels</span>
            <span className="totals__value">{formatMoney(totals.lineExtension, currency)}</span>
          </div>

          {documentAdjustments.map((adjustment, index) => (
            <div className="totals__row" key={`adjustment-${index}`}>
              <span>
                {adjustment.isCharge ? "Toeslag" : "Korting"}
                {adjustment.reason ? `: ${adjustment.reason}` : ""}
              </span>
              <span className="totals__value">
                {adjustment.isCharge ? "" : "-"}
                {formatMoney(adjustment.amount, currency)}
              </span>
            </div>
          ))}

          <div className="totals__row">
            <span>Totaal exclusief btw</span>
            <span className="totals__value">{formatMoney(totals.taxExclusive, currency)}</span>
          </div>

          {totals.vatBreakdown.map((bucket, index) => {
            const path = `vatBreakdown[${index}].exemptionReason`;
            return (
              <div key={`${bucket.category}-${bucket.rate}`}>
                <div className="totals__row">
                  <span>
                    Btw {bucket.category} {bucket.category === "O" ? "" : `${bucket.rate}% `}over{" "}
                    {formatMoney(bucket.taxable, currency)}
                  </span>
                  <span className="totals__value">{formatMoney(bucket.tax, currency)}</span>
                </div>
                {NEEDS_REASON.has(bucket.category) && (
                  <input
                    id={path}
                    className={`cell-input ${invalid.has(path) ? "input--invalid" : ""}`.trim()}
                    type="text"
                    placeholder="Vrijstellingsreden, bijv. 'Btw verlegd'"
                    aria-label={`Vrijstellingsreden btw-categorie ${bucket.category}`}
                    readOnly={readOnly}
                    value={reasonFor(bucket.category, bucket.rate)}
                    onChange={(event) => onReason(bucket.category, bucket.rate, event.target.value)}
                  />
                )}
              </div>
            );
          })}

          {totals.prepaid !== null && (
            <div className="totals__row">
              <span>Vooruitbetaald</span>
              <span className="totals__value">-{formatMoney(totals.prepaid, currency)}</span>
            </div>
          )}

          <div className="totals__row totals__row--grand">
            <span>Te betalen</span>
            <span className="totals__value">{formatMoney(totals.payable, currency)}</span>
          </div>
        </div>
      </div>
    </div>
  );
}
