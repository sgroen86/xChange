import type { Dictionary } from "../content/i18n/types";
import { Icon } from "./Icon";

/**
 * Illustration for the hero: a stylised PDF turning into a validated
 * e-invoice. Pure HTML/CSS, decorative except for its accessible label.
 */
export function HeroMock({ mock }: { mock: Dictionary["home"]["mock"] }) {
  return (
    <figure className="hero-mock" aria-label={mock.label}>
      <div className="hero-mock__pdf" aria-hidden="true">
        <div className="hero-mock__pdf-head">
          <Icon name="file" />
          <span>{mock.fileName}</span>
        </div>
        <div className="hero-mock__lines">
          <span style={{ width: "62%" }} />
          <span style={{ width: "40%" }} />
          <span style={{ width: "78%" }} />
          <span style={{ width: "55%" }} />
          <span style={{ width: "70%" }} />
        </div>
        <span className="hero-mock__pdf-tag">PDF</span>
      </div>

      <div className="hero-mock__arrow" aria-hidden="true">
        <Icon name="sparkles" />
        <span>{mock.reading}</span>
      </div>

      <div className="hero-mock__card">
        <div className="hero-mock__card-head">
          <span className="hero-mock__format">{mock.format}</span>
          <span className="hero-mock__valid">
            <Icon name="check" />
            {mock.valid}
          </span>
        </div>
        <dl className="hero-mock__fields">
          <div>
            <dt>{mock.customer}</dt>
            <dd>{mock.customerValue}</dd>
          </div>
          <div>
            <dt>{mock.vatNumber}</dt>
            <dd className="mono">BE0123456789</dd>
          </div>
          <div>
            <dt>{mock.peppolId}</dt>
            <dd className="mono">0208:0123456789</dd>
          </div>
          <div>
            <dt>{mock.total}</dt>
            <dd className="mono">{mock.totalValue}</dd>
          </div>
        </dl>
        <p className="hero-mock__receives">
          <Icon name="shieldCheck" />
          {mock.receives}
        </p>
        <span className="hero-mock__download" aria-hidden="true">
          <Icon name="fileCheck" />
          {mock.download}
        </span>
      </div>
    </figure>
  );
}
