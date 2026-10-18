"use client";

import { useText } from "../../../../lib/i18n";
import type { ReceiverLookup } from "../../../../lib/invoices";
import type { RulesetInfo } from "../../../../lib/settings";

interface Props {
  lookup: ReceiverLookup | null;
  looking: boolean;
  rulesets: RulesetInfo[];
  /** The buyer's electronic address as the invoice has it now, "0106:12345678". */
  current: string | null;
  /** True when xChange filled the electronic address from the network. */
  filled: boolean;
  readOnly: boolean;
  onAdopt: () => void;
}

/**
 * Whether the buyer can receive e-invoices: looked up on PEPPOL by its
 * numbers while the invoice is reviewed. Informative only; it never blocks.
 */
export function ReceiverStatus({ lookup, looking, rulesets, current, filled, readOnly, onAdopt }: Props) {
  const text = useText().review.receiver;

  if (looking && !lookup) {
    return <p className="text-soft text-sm">{text.checking}</p>;
  }

  if (!lookup) return null;

  const nameOf = (id: string) => rulesets.find((r) => r.id === id)?.displayName ?? id;

  switch (lookup.status) {
    case "found": {
      const address = lookup.participant ? `${lookup.participant.scheme}:${lookup.participant.value}` : "";
      const formats = lookup.acceptedRulesetIds.map(nameOf);
      return (
        <div className="alert alert--success">
          <div>
            <strong>{text.onPeppol}</strong> {text.as} <code>{address}</code>
            {filled && current?.toLowerCase() === address.toLowerCase() && text.filled}.{" "}
            {formats.length > 0
              ? text.receives(formats.join(", "))
              : text.receivesNone}
            {current !== null && current.toLowerCase() !== address.toLowerCase() && !readOnly && (
              <>
                {" "}
                <button type="button" className="btn btn--secondary btn--sm" onClick={onAdopt}>
                  {text.adopt}
                </button>
              </>
            )}
          </div>
        </div>
      );
    }
    case "notFound":
      return (
        <div className="alert alert--warning">
          <div>{text.notFound(lookup.tried.join(", "))}</div>
        </div>
      );
    case "unavailable":
      return <p className="text-soft text-sm">{text.unavailable}</p>;
    default:
      return <p className="text-soft text-sm">{text.nothingToLookUp}</p>;
  }
}
