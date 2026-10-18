"use client";

import { Icon } from "../../../../components/Icon";
import type { CanonicalInvoiceLine } from "../../../../lib/canonical";
import { useText } from "../../../../lib/i18n";
import { CellInput, CellSelect, unitCodes, type Option } from "./fields";

interface Props {
  lines: CanonicalInvoiceLine[];
  /** Calculated net per line, in line order. */
  nets: (string | null)[];
  /** The tax categories of the chosen format, and its tax name ("Btw"/"VAT", "GST"). */
  taxCategories: Option[];
  taxName: string;
  readOnly: boolean;
  onAdd: () => void;
  onRemove: (index: number) => void;
}

export function LinesCard({ lines, nets, taxCategories, taxName, readOnly, onAdd, onRemove }: Props) {
  const t = useText();
  const text = t.review.lines;
  return (
    <div className="card">
      <div className="card__header">
        <h3>{text.title}</h3>
        {!readOnly && (
          <button type="button" className="btn btn--secondary btn--sm" onClick={onAdd}>
            <Icon name="filePlus" /> {text.add}
          </button>
        )}
      </div>
      <div className="card__body card__body--flush">
        <div className="table-wrap">
          <table className="table lines-table">
            <thead>
              <tr>
                <th>{text.description}</th>
                <th>{text.quantity}</th>
                <th>{text.unit}</th>
                <th>{text.price}</th>
                <th>{taxName}</th>
                <th>{taxName} %</th>
                <th className="cell-amount">{text.net}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {lines.map((line, index) => (
                // Lines have no stable id until booked; the index is the line's identity here.
                <tr key={index}>
                  <td>
                    <CellInput path={`lines[${index}].description`} label={text.description} />
                    {line.allowancesAndCharges.length > 0 && (
                      <div className="text-xs text-muted">{text.adjustments(line.allowancesAndCharges.length)}</div>
                    )}
                  </td>
                  <td>
                    <CellInput path={`lines[${index}].quantity`} label={text.quantity} amount />
                  </td>
                  <td>
                    <CellSelect path={`lines[${index}].unitCode`} label={text.unit} options={unitCodes(t)} />
                  </td>
                  <td>
                    <CellInput path={`lines[${index}].unitPrice`} label={text.price} amount />
                  </td>
                  <td>
                    <CellSelect path={`lines[${index}].vatCategoryCode`} label={text.taxCategory(taxName)} options={taxCategories} />
                  </td>
                  <td>
                    <CellInput path={`lines[${index}].vatPercentage`} label={text.taxPercentage(taxName)} amount />
                  </td>
                  <td className="cell-amount">{nets[index] ?? "—"}</td>
                  <td>
                    {!readOnly && (
                      <button
                        type="button"
                        className="btn btn--ghost btn--icon"
                        onClick={() => onRemove(index)}
                        aria-label={text.remove}
                      >
                        <Icon name="trash" />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
