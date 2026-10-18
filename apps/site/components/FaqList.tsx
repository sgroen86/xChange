import { Icon } from "./Icon";

/** Accordion of questions, built on <details>/<summary>: works without JavaScript. */
export function FaqList({ items }: { items: { q: string; a: string }[] }) {
  return (
    <div className="faq">
      {items.map((item) => (
        <details key={item.q} className="faq__item">
          <summary>
            <span>{item.q}</span>
            <Icon name="chevronDown" className="faq__chevron" />
          </summary>
          <p>{item.a}</p>
        </details>
      ))}
    </div>
  );
}
