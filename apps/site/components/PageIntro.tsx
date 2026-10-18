import type { ReactNode } from "react";

/** Title block at the top of an inner page. */
export function PageIntro({
  eyebrow,
  title,
  lead,
  wide = false,
  children,
}: {
  /** Align with full-width content below (card grids) instead of a narrow column. */
  wide?: boolean;
  eyebrow?: string;
  title: string;
  lead?: string;
  children?: ReactNode;
}) {
  return (
    <section className="page-intro">
      <div className={wide ? "container" : "container container--narrow"}>
        {eyebrow && <p className="eyebrow">{eyebrow}</p>}
        <h1>{title}</h1>
        {lead && <p className="lead">{lead}</p>}
        {children}
      </div>
    </section>
  );
}
