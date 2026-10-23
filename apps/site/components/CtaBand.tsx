import Link from "next/link";

import { Icon } from "./Icon";

export function CtaBand({
  title,
  text,
  primary,
  secondary,
}: {
  title: string;
  text: string;
  primary: { href: string; label: string };
  secondary?: { href: string; label: string };
}) {
  return (
    <section className="section">
      <div className="container">
        <div className="cta-band">
          <div>
            <h2>{title}</h2>
            <p>{text}</p>
          </div>
          <div className="cta-band__actions">
            {/* Sign-up lives in the app, on another origin: a plain link. */}
            {/^https?:\/\//.test(primary.href) ? (
              <a href={primary.href} className="btn btn--primary btn--lg">
                {primary.label}
                <Icon name="arrowRight" />
              </a>
            ) : (
              <Link href={primary.href} className="btn btn--primary btn--lg">
                {primary.label}
                <Icon name="arrowRight" />
              </Link>
            )}
            {secondary && (
              <Link href={secondary.href} className="btn btn--secondary btn--lg">
                {secondary.label}
              </Link>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}
