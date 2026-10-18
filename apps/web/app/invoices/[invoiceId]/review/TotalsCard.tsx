"use client";

import type { CanonicalAllowanceCharge } from "../../../../lib/canonical";
import { useText } from "../../../../lib/i18n";
import { formatMoney } from "../../../../lib/money";
import type { CalculatedTotals } from "../../../../lib/totals";

/** Categories whose VAT breakdown must carry an exemption reason (BR-E-10 and friends). */
const NEEDS_REASON = new Set(["E", "AE", "G", "O"]);

interface Props {
  totals: CalculatedTotals;
  currency: string | null;
  /** "Btw"/"VAT" or "GST". */
  taxName: string;
  /** Set when the totals must also be stated in the tax currency (PINT-SG: SGD). */
  taxCurrency: string | null;
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
export function TotalsCard(props: Props) {
  const { totals, currency, taxName, taxCurrency, documentAdjustments, reasonFor, onReason, invalid, readOnly } = props;
  const inTaxCurrency = taxCurrency ? totals.inTaxCurrency : null;
  const text = useText().review.totals;
  return (
    <div className="card">
      <div className="card__header">
        <h3>{text.title}</h3>
        <span className="badge badge--neutral">{text.calculated}</span>
      </div>
      <div className="card__body">
        <div className="totals">
          <div className="totals__row">
            <span>{text.lines}</span>
            <span className="totals__value">{formatMoney(totals.lineExtension, currency)}</span>
          </div>

          {documentAdjustments.map((adjustment, index) => (
            <div className="totals__row" key={`adjustment-${index}`}>
              <span>
                {adjustment.isCharge ? text.charge : text.allowance}
                {adjustment.reason ? `: ${adjustment.reason}` : ""}
              </span>
              <span className="totals__value">
                {adjustment.isCharge ? "" : "-"}
                {formatMoney(adjustment.amount, currency)}
              </span>
            </div>
          ))}

          <div className="totals__row">
            <span>{text.exclusive(taxName)}</span>
            <span className="totals__value">{formatMoney(totals.taxExclusive, currency)}</span>
          </div>

          {totals.vatBreakdown.map((bucket, index) => {
            const path = `vatBreakdown[${index}].exemptionReason`;
            return (
              <div key={`${bucket.category}-${bucket.rate}`}>
                <div className="totals__row">
                  <span>
                    {taxName} {bucket.category} {bucket.category === "O" ? "" : `${bucket.rate}% `}{text.over}{" "}
                    {formatMoney(bucket.taxable, currency)}
                  </span>
                  <span className="totals__value">{formatMoney(bucket.tax, currency)}</span>
                </div>
                {NEEDS_REASON.has(bucket.category) && (
                  <input
                    id={path}
                    className={`cell-input ${invalid.has(path) ? "input--invalid" : ""}`.trim()}
                    type="text"
                    placeholder={text.reasonPlaceholder}
                    aria-label={text.reasonLabel(bucket.category)}
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
              <span>{text.prepaid}</span>
              <span className="totals__value">-{formatMoney(totals.prepaid, currency)}</span>
            </div>
          )}

          <div className="totals__row totals__row--grand">
            <span>{text.payable}</span>
            <span className="totals__value">{formatMoney(totals.payable, currency)}</span>
          </div>

          {inTaxCurrency && taxCurrency && (
            <>
              <div className="totals__row">
                <span>{text.exclusiveIn(taxName, taxCurrency)}</span>
                <span className="totals__value">{formatMoney(inTaxCurrency.exclusive, taxCurrency)}</span>
              </div>
              <div className="totals__row">
                <span>{text.taxIn(taxName, taxCurrency)}</span>
                <span className="totals__value">{formatMoney(inTaxCurrency.tax, taxCurrency)}</span>
              </div>
              <div className="totals__row">
                <span>{text.inclusiveIn(taxName, taxCurrency)}</span>
                <span className="totals__value">{formatMoney(inTaxCurrency.inclusive, taxCurrency)}</span>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
