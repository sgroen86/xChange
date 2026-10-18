import type { Dictionary } from "../content/i18n/types";
import type { LegalDoc } from "../content/legal/types";
import { Icon } from "./Icon";

/** Renders a legal draft: draft banner, title, contents and numbered sections. */
export function LegalDocument({
  doc,
  labels,
  updated,
}: {
  doc: LegalDoc;
  labels: Dictionary["legal"];
  updated: string;
}) {
  return (
    <article className="legal">
      <div className="container container--prose">
        <p className="draft-note" role="note">
          <Icon name="info" />
          {labels.draftNote}
        </p>
        <header className="legal__header">
          <h1>{doc.title}</h1>
          <p className="legal__updated">
            {labels.lastUpdated} {updated}
          </p>
          {doc.intro.map((p) => (
            <p key={p} className="lead">
              {p}
            </p>
          ))}
        </header>

        <nav className="legal__toc" aria-labelledby="legal-toc">
          <h2 id="legal-toc">{labels.contents}</h2>
          <ol>
            {doc.sections.map((section, i) => (
              <li key={section.heading}>
                <a href={`#section-${i + 1}`}>{section.heading}</a>
              </li>
            ))}
          </ol>
        </nav>

        <div className="prose">
          {doc.sections.map((section, i) => (
            <section key={section.heading} id={`section-${i + 1}`} className="legal__section">
              <h2>
                <span className="legal__number">{i + 1}.</span> {section.heading}
              </h2>
              {section.paragraphs?.map((p) => <p key={p}>{p}</p>)}
              {section.items && (
                <ul>
                  {section.items.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              )}
              {section.table && (
                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        {section.table.head.map((cell) => (
                          <th key={cell} scope="col">
                            {cell}
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {section.table.rows.map((row) => (
                        <tr key={row.join("|")}>
                          {row.map((cell, j) =>
                            j === 0 ? (
                              <th key={j} scope="row">
                                {cell}
                              </th>
                            ) : (
                              <td key={j}>{cell}</td>
                            ),
                          )}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              {section.after?.map((p) => <p key={p}>{p}</p>)}
            </section>
          ))}
        </div>
      </div>
    </article>
  );
}
