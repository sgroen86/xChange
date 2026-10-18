/**
 * Which format the export menu opens on. Pure, so it is tested without a
 * browser. In order: the user's choice for this download, the format set on
 * the customer, a format the receiver accepts on its network (the
 * organisation's own default when the receiver takes it), the organisation's
 * default.
 */

export type FormatSource = "chosen" | "customer" | "receiver" | "organization";

export interface FormatChoice {
  id: string;
  source: FormatSource;
}

export function chooseFormat(input: {
  chosen: string | null;
  customerFormat: string | null;
  /** Formats the receiver accepts, in the order the menu lists them; null when unknown. */
  receiverAccepts: string[] | null;
  organizationDefault: string;
}): FormatChoice {
  if (input.chosen) return { id: input.chosen, source: "chosen" };
  if (input.customerFormat) return { id: input.customerFormat, source: "customer" };

  const accepts = input.receiverAccepts;
  if (accepts && accepts.length > 0 && !accepts.includes(input.organizationDefault)) {
    return { id: accepts[0], source: "receiver" };
  }

  return { id: input.organizationDefault, source: "organization" };
}
