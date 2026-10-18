import { Logo } from "../../components/Logo";
import { languageNames, langs } from "../../content/langs";

/**
 * "/" sends the visitor to their language: nl -> /nl/, fr -> /fr/, any other
 * detectable language -> /en/, nothing detectable -> /nl/. The script runs
 * as soon as the HTML is parsed; the links are the fallback without it.
 */
const redirect = `(function(){try{var l=(navigator.language||"").toLowerCase();var t=l.indexOf("nl")===0?"/nl/":l.indexOf("fr")===0?"/fr/":l?"/en/":"/nl/";window.location.replace(t);}catch(e){}})();`;

export default function LanguageChooser() {
  return (
    <main className="chooser">
      <script dangerouslySetInnerHTML={{ __html: redirect }} />
      <Logo />
      <noscript>
        <p className="chooser__note">Kies je taal · Choose your language · Choisissez votre langue</p>
      </noscript>
      <ul className="chooser__list">
        {langs.map((lang) => (
          <li key={lang}>
            <a href={`/${lang}/`} hrefLang={lang} lang={lang} className="btn btn--secondary">
              {languageNames[lang]}
            </a>
          </li>
        ))}
      </ul>
    </main>
  );
}
