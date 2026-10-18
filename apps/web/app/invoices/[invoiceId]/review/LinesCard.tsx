"use client";

import { Icon } from "../../../../components/Icon";
import type { CanonicalInvoiceLine } from "../../../../lib/canonical";
import { CellInput, CellSelect, UNIT_CODES, VAT_CATEGORIES } from "./fields";

interface Props {
  lines: CanonicalInvoiceLine[];
  /** Calculated net per line, in line order. */
  nets: (string | null)[];
  readOnly: boolean;
  onAdd: () => void;
  onRemove: (index: number) => void;
}

export function LinesCard({ lines, nets, readOnly, onAdd, onRemove }: Props) {
  return (
    <div className="card">
      <div className="card__header">
        <h3>Factuurregels</h3>
        {!readOnly && (
          <button type="button" className="btn btn--secondary btn--sm" onClick={onAdd}>
            <Icon name="filePlus" /> Regel toevoegen
          </button>
        )}
      </div>
      <div className="card__body card__body--flush">
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>Omschrijving</th>
                <th>Aantal</th>
                <th>Eenheid</th>
                <th>Prijs</th>
                <th>Btw</th>
                <th>Btw %</th>
                <th className="cell-amount">Netto</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {lines.map((line, index) => (
                // Lines have no stable id until booked; the index is the line's identity here.
                <tr key={index}>
                  <td>
                    <CellInput path={`lines[${index}].description`} label="Omschrijving" />
                    {line.allowancesAndCharges.length > 0 && (
                      <div className="text-xs text-muted">
                        Inclusief {line.allowancesAndCharges.length} korting(en)/toeslag(en) van de factuur.
                      </div>
                    )}
                  </td>
                  <td>
                    <CellInput path={`lines[${index}].quantity`} label="Aantal" amount />
                  </td>
                  <td>
                    <CellSelect path={`lines[${index}].unitCode`} label="Eenheid" options={UNIT_CODES} />
                  </td>
                  <td>
                    <CellInput path={`lines[${index}].unitPrice`} label="Prijs" amount />
                  </td>
                  <td>
                    <CellSelect path={`lines[${index}].vatCategoryCode`} label="Btw-categorie" options={VAT_CATEGORIES} />
                  </td>
                  <td>
                    <CellInput path={`lines[${index}].vatPercentage`} label="Btw-percentage" amount />
                  </td>
                  <td className="cell-amount">{nets[index] ?? "—"}</td>
                  <td>
                    {!readOnly && (
                      <button
                        type="button"
                        className="btn btn--ghost btn--icon"
                        onClick={() => onRemove(index)}
                        aria-label="Regel verwijderen"
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
