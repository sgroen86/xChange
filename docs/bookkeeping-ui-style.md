# Bookkeeping UI Style Reference

Inspection of the existing Bookkeeping application, recorded so xChange can match it instead of
inventing a second visual language. **This is a description of what exists, not a proposal.** No
Bookkeeping file was modified.

Bookkeeping app root: `E:\greenitsolutions\Bookkeeping` (paths below are relative to it).

## 1. Stack and build tooling

| Concern | Reality |
|---|---|
| Backend | PHP 8.1+, CodeIgniter 4 (`composer.json`) |
| Frontend | **No framework.** Hand-written ES modules, native `<script type="module">` |
| Build step | **None.** No `package.json`, no bundler, no transpiler — the browser loads source directly |
| CSS framework | **None.** Hand-written CSS with custom properties |
| Component library | **None.** Components are functions returning HTML strings |
| Only third-party JS | Chart.js 4.4.7 via cdnjs, loaded globally in the shell |
| Test suite | **None.** No `tests/` directory, no `phpunit.xml`; `composer test` is declared but has nothing to run |

Shell/entry: `app/Views/spa_shell.php` renders an empty `<div class="app" id="app">` and boots
`js/app.js`. Cache-busting is `?v=<?= time() ?>` on every CSS/JS link, recomputed per request.

## 2. Critical duplication hazards

**Do not replicate these patterns in xChange.**

- `css/` and `public/css/` hold **byte-identical copies** of all nine stylesheets. `js/` and
  `public/js/` are likewise duplicated — and have already **diverged**: `currencies.js` exists only
  in `public/js/`. Editing one copy silently does nothing.
- `js/app.js` imports **every** view module eagerly at boot, so adding a view adds startup cost for
  all users. Do not carry this pattern over to xChange's routing.

## 3. Design tokens

Single source: `css/variables.css`, all on `:root`. **Dark theme only** — there is no light theme
and no `prefers-color-scheme` handling anywhere in the app.

```css
/* Typography */
--font-display: 'DM Serif Display', serif;      /* h1-h4 and stat values */
--font-body:    'IBM Plex Sans', -apple-system, system-ui, sans-serif;
--font-mono:    'IBM Plex Mono', 'SF Mono', monospace;   /* amounts, codes */

--text-xs: 0.8rem;  --text-sm: 0.875rem;  --text-base: 1rem;
--text-lg: 1.15rem; --text-xl: 1.5rem;    --text-2xl: 2rem;

/* Surfaces (dark) */
--color-bg:             #0b1120;   /* page background */
--color-surface:        #111827;   /* cards, panels */
--color-surface-raised: #1a2332;   /* inputs, secondary buttons */
--color-surface-sunken: #0d1526;   /* table headers, tab track */
--color-surface-hover:  #1e2d3d;

/* Text */
--color-ink: #e2e8f0;  --color-ink-soft: #94a3b8;  --color-ink-muted: #64748b;

/* Borders */
--color-border: #1e2d3d;  --color-border-light: #162032;

/* Brand — gold accent */
--color-primary:      #d4a843;
--color-primary-soft: rgba(212,168,67,0.2);   /* focus rings */
--color-primary-bg:   rgba(212,168,67,0.08);

/* Semantic — each has a matching translucent -bg variant */
--color-success: #34d399;   --color-danger:  #f87171;
--color-warning: #fbbf24;   --color-info:    #60a5fa;

/* Spacing: --space-1|2|3|4|5|6|8 = 4|8|12|16|20|24|32 px */
/* Radius:  --radius-sm|md|lg|xl  = 6|8|12|16 px */
/* Layout:  --sidebar-width 260px, --sidebar-collapsed 64px, --header-height 60px */
/* Shadows: --shadow-sm|md|lg   Transitions: --transition-fast .15s, --transition-base .25s */
```

Fonts load from Google Fonts in `spa_shell.php` (IBM Plex Sans 400/500/600/700, DM Serif Display,
IBM Plex Mono 400/500).

The radius tokens are **inconsistently applied**: cards hardcode `14px` and buttons/inputs hardcode
`10px` rather than using `--radius-*`. Match the hardcoded values for visual consistency; don't
"fix" them in Bookkeeping.

## 4. Stylesheet map

Load order is fixed in `spa_shell.php` and matters:

| File | Contains |
|---|---|
| `css/variables.css` | all design tokens |
| `css/base.css` | reset, typography, utilities, **cards, stat cards, badges, tabs, page header, grids** |
| `css/layout.css` | `.app`, header, content, company switcher, search overlay, notifications |
| `css/sidebar.css` | sidebar, nav items, collapse behaviour, mobile drawer |
| `css/buttons.css` | `.btn` and variants |
| `css/forms.css` | inputs, selects, textareas, dropzone, toggle |
| `css/tables.css` | `.table`, amount cells |
| `css/modals.css` | modal, backdrop, **toasts** |
| `css/inbox.css` | inbox split-pane, extraction cards (invoice-specific) |

Cards, badges and tabs live in `base.css`, not in their own files — don't go looking for `cards.css`.

## 5. Global layout

`.app` is a grid: sidebar column + main column. `.app__main` holds `.header` (60px, sticky) and
`.content` → `.content__inner`, which is the routed view's mount point. `body` is
`overflow: hidden`; the content column scrolls, not the page.

`.app--sidebar-collapsed` on the root collapses the sidebar to 64px (icons only).

The header (`css/layout.css`) provides breadcrumb/title, date, a company switcher with dropdown, a
search overlay (`.search-overlay`, keyboard-driven), a notification dropdown, and a mobile menu
toggle. At ≤768px the sidebar becomes an overlay drawer backed by `.sidebar-overlay`.

## 6. Navigation

Defined declaratively in `js/components/sidebar.js` as `navStructure` — a flat array mixing section
separators and items:

```js
{ section: 'Boekhouden' },
{ id: 'intake', label: 'Inkomende Facturen', icon: 'fileDown' },
{ id: 'herhalend', label: '...', icon: 'repeat', badge: 'auto', hidden: true },
```

Item fields: `id` (doubles as the route), `label`, `icon` (key into the icon set), optional `badge`
(a number, or `'auto'` for the sparkle badge), optional `hidden` to keep an item out of the UI
without deleting it. Sections in use: Boekhouden, Overzichten, Aangiftes, Factureren, Uren,
Relaties, Automatisering, Beheer. **All labels are Dutch.**

Routing lives in `js/router.js`: `registerRoute(path, renderer)`, `navigate(path)`,
`getCurrentRoute()`, `onNavigate(cb)`, `initRouter(containerId)`. Each view module in `js/views/`
default-exports a render function.

## 7. Components xChange should reuse

Port the **markup contract and CSS** of these; do not invent parallel equivalents.

| Component | Path | API |
|---|---|---|
| Icons | `js/components/icons.js` | `icon(name, className)` → inline SVG string |
| Toasts | `js/components/toast.js` | `showToast(message, type, duration = 3500)`; types `success \| danger \| warning \| info` |
| Modal | `js/components/modal.js` | `showModal({title, body, size, actions, onClose})`, `closeModal()`; sizes `default \| sm \| md \| lg \| xl` |
| Sidebar | `js/components/sidebar.js` | `renderSidebar()`, `initSidebar()`, `updateSidebarActive(route)` |
| Header | `js/components/header.js` | `renderHeader(route)`, `updateHeader(route)`, `initHeader()` |
| Router | `js/router.js` | see above |
| Formatting | `js/helpers.js` | `formatEUR`, `formatDate`, `formatDateRelative`, `uid` |

### Icons

Lucide-style inline SVGs: `viewBox="0 0 24 24"`, `stroke="currentColor"`, `stroke-width="2"`, round
caps and joins. No icon font, no icon package. Available keys:

`menu dashboard book fileDown fileUp bank mail settings plus search filter download upload check
clock x chevronLeft chevronRight chevronDown arrowUpDown sparkles paperclip eye trash edit moreH
bell user link trendingUp euro panelLeft users repeat phone messageCircle fileText package scale
building pieChart barChart clipboard globe archive zap calendar hardDrive filePlus fileMinus tag
send folder list save copy star info`

Sizing is contextual: `.btn svg` 18px, `.btn--sm svg` 14px, `.page-header h1 svg` 1.1em. Add new
icons to this file in the same style rather than pulling in an icon library.

## 8. Page and component conventions

**Page shell** — every view opens with:

```html
<div class="page-header">
  <div>
    <h1><!-- icon('fileText') --> Title</h1>
    <p class="page-header__subtitle" id="...Subtitle">Laden...</p>
  </div>
  <div class="page-header__actions"><!-- buttons --></div>
</div>
```

**Cards**: `.card` > `.card__header` (title + actions) + `.card__body`. Use `.card__body--flush`
(zero padding) when the body is a table. `.stats-row` is a 4-column grid of `.stat-card`
(`__label` uppercase muted, `__value` in the display font, `__change`).

**Badges**: `.badge` plus `--success | --danger | --warning | --info | --neutral`.

**Tabs**: `.tabs` (pill track) > `.tab`, active is `.tab--active`. Buttons, not links.

**Buttons**: `.btn` plus `--primary` (gold with dark text), `--secondary`, `--ghost`, `--danger`.
Sizes `--sm` and `--icon`. `.btn-group` for segmented controls; `.btn--secondary.active` marks the
selected item in a toggle group.

**Forms**: two conventions coexist — `.form-group`/`.form-label`/`.form-input` and BEM
`.form .form__row`/`.form__group`/`.form__actions`. `forms.css` additionally styles bare
`.form input[type=...]` and `.card__body label`, so unclassed inputs inside a `.form` still render
correctly. Focus state everywhere: primary border plus `0 0 0 3px var(--color-primary-soft)`.
Also available: `.dropzone` (file upload) and `.toggle` / `.toggle--active` (switch).

**Tables**: `.table` — uppercase muted `thead` on the sunken surface, row hover, `.table--clickable`
for the cursor, `tfoot` with a heavier top border. **Money cells use `.cell-amount`**: right
aligned, mono font, `font-variant-numeric: tabular-nums`. Wrap in `.table-wrap` or
`.card__body--flush` so mobile can scroll it.

## 9. Loading, errors and warnings

- **Loading**: there is **no spinner and no skeleton CSS anywhere in the app**. The convention is a
  literal Dutch text placeholder — `Laden...` — in `.page-header__subtitle`, or a full-width `<tr>`
  with `class="text-soft"` inside the table body, replaced once data arrives.
- **Errors and warnings**: surfaced via `showToast(msg, 'danger')` / `'warning'`, typically from a
  `catch` block or an `if (!j.ok)` check on a JSON response. Messages are Dutch and user-facing
  (`'Opslaan mislukt'`, `'Netwerk fout bij verwijderen'`). Toasts stack in `.toast-container` and
  auto-dismiss after 3500ms.
- There is no inline field-level validation styling and no error-boundary equivalent.

## 10. Responsive breakpoints

Only three, all `max-width`:

| Breakpoint | Effect |
|---|---|
| **900px** | `.grid-2` collapses to one column |
| **768px** | Primary mobile breakpoint: sidebar becomes an overlay drawer, stats go 4→2 columns, page header stacks, tables scroll horizontally (`.table` gets `min-width: 600px`), form rows stack, tabs scroll |
| **480px** | Stats collapse to a single column |

Mobile rules live at the bottom of `base.css`, `layout.css`, `sidebar.css` and `modals.css`.

## 11. Existing xChange invoice code — read before writing any Domain code

`app/Xhange/` already contains a **working PHP implementation of the canonical invoice model**. It
is independent of CodeIgniter and **not referenced anywhere else in the application** — a library
that has not been wired in.

- `Canonical/` — `CanonicalInvoice` (immutable, `readonly` promoted properties, `with()`,
  `toArray()`, `isCreditNote()`), plus `Party`, `Address`, `Contact`, `InvoiceLine`, `ItemInfo`,
  `Totals`, `VatBreakdownLine`, `VatCategory`, `AllowanceCharge`, `PaymentDetails`,
  `DocumentReferences`, `BusinessTerms`, `ExtractionMeta`, `FieldProvenance`
- `Support/Decimal.php` — integer-scaled decimal arithmetic (`of`, `ofUnits`, `add`, `subtract`,
  `multiply`, `divide`, `percentage`, `sum`, `compareTo`, scale conversion). Deliberately avoids
  floats.
- `Pipeline/InvoiceCalculator.php` — derives line net amounts, the VAT breakdown and document
  totals; returns `CalculationResult`
- `Validation/` — `ValidationReport` (`errors()`, `warnings()`, `isValid()`, `forPath()`) and
  `ValidationIssue`
- `Support/Normalizer.php`, `Support/Arr.php`

xChange's .NET `InvoicePlatform.Domain` covers **the same problem space**. `Decimal.php` in
particular is a manual reimplementation of what .NET's `decimal` provides natively. Treat this
directory as the **specification** for field names, VAT bucketing rules, rounding behaviour and
provenance tracking — but raise the port explicitly rather than translating it line by line, and
confirm which implementation is authoritative before duplicating the calculation logic in C#.

`FieldProvenance` and `ExtractionMeta` map directly onto the confidence/review step in xChange's
`CanonicalInvoiceDraft` → `ValidatedCanonicalInvoice` flow.

Related, not yet inspected: `migrations_phase10_xhange.sql` and the phase 7/8/9 migrations
(intake, OCR, multicurrency) in the Bookkeeping root.

## 12. Rules for xChange

1. **Do not add a CSS framework** (no Tailwind, no Bootstrap) and **do not add a component library**
   (no MUI, shadcn, Chakra). The `apps/web` scaffold was created with `--no-tailwind` for this reason.
2. Port `variables.css` as the token source and build on plain CSS or CSS Modules. Keep token names
   identical so the two apps stay visually aligned.
3. Reuse the class contracts above (`.card`, `.btn`, `.table`, `.badge`, `.tabs`, `.form-*`) rather
   than inventing parallel names.
4. Keep one copy of every asset. Do not recreate the `css/` ↔ `public/css/` mirror.
5. Money renders with `.cell-amount` semantics: mono, right aligned, tabular figures.
6. Icons come from the existing Lucide-style inline set — extend it, don't add a package.
7. Bookkeeping's UI is Dutch. Decide xChange's UI language explicitly before writing copy.
