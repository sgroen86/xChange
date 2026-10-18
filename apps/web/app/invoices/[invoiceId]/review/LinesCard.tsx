"use client";

import { Icon } from "../../../../components/Icon";
import type { CanonicalInvoiceLine } from "../../../../lib/canonical";
import { CellInput, CellSelect, UNIT_CODES, type Option } from "./fields";

interface Props {
  lines: CanonicalInvoiceLine[];
  /** Calculated net per line, in line order. */
  nets: (string | null)[];
  /** The tax categories of the chosen format, and its tax name ("Btw", "GST"). */
  taxCategories: Option[];
  taxName: string;
  readOnly: boolean;
  onAdd: () => void;
  onRemove: (index: number) => void;
}

export function LinesCard({ lines, nets, taxCategories, taxName, readOnly, onAdd, onRemove }: Props) {
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
          <table className="table lines-table">
            <thead>
              <tr>
                <th>Omschrijving</th>
                <th>Aantal</th>
                <th>Eenheid</th>
                <th>Prijs</th>
                <th>{taxName}</th>
                <th>{taxName} %</th>
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
                    <CellSelect path={`lines[${index}].vatCategoryCode`} label={`${taxName}-categorie`} options={taxCategories} />
                  </td>
                  <td>
                    <CellInput path={`lines[${index}].vatPercentage`} label={`${taxName}-percentage`} amount />
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
