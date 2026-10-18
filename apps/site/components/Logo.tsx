/** The xChange wordmark: a gold "x" mark and the name in the display face. */
export function Logo() {
  return (
    <span className="logo">
      <span className="logo__mark" aria-hidden="true">
        <svg viewBox="0 0 32 32" focusable="false">
          <rect width="32" height="32" rx="8" />
          <path d="M10 10l12 12M22 10L10 22" />
        </svg>
      </span>
      <span className="logo__text">
        <span className="logo__x">x</span>Change
      </span>
    </span>
  );
}
