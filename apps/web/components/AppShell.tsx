"use client";

/**
 * Application shell — the same structure as Bookkeeping's spa_shell.php plus
 * js/components/sidebar.js and header.js: fixed sidebar, sticky translucent
 * header, single scrolling content column, mobile drawer below 768px.
 */

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState, useSyncExternalStore } from "react";
import { isApiConfigured } from "../lib/api";
import {
  clearSession,
  getServerUserSnapshot,
  getToken,
  getUserSnapshot,
  logout,
  subscribeToUser,
} from "../lib/auth";
import { Icon } from "./Icon";
import { isSection, navStructure, pageTitles, type NavItem } from "./nav";

function currentInvoiceId(pathname: string): string | null {
  const match = pathname.match(/\/invoices\/([^/]+)\/review/);
  return match ? match[1] : null;
}

function activeNavId(pathname: string): string {
  if (/\/invoices\/[^/]+\/review/.test(pathname)) return "review";
  if (pathname.includes("/invoices")) return "invoices";
  return "upload";
}

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname() ?? "";
  const router = useRouter();
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);

  const onLoginPage = pathname.startsWith("/login");

  /* The session is an external store, not React state. useSyncExternalStore
     reads it without an effect and re-renders when it changes; the server
     snapshot is null, so the prerendered HTML shows the signed-out shell. */
  const user = useSyncExternalStore(subscribeToUser, getUserSnapshot, getServerUserSnapshot);

  useEffect(() => {
    // With no API configured the app runs on mock data and needs no login.
    if (!isApiConfigured || onLoginPage) return;

    if (!getToken()) {
      router.replace("/login");
    }
  }, [onLoginPage, router]);

  const signOut = async () => {
    await logout();
    clearSession();
    router.replace("/login");
  };

  const invoiceId = currentInvoiceId(pathname);
  const activeId = activeNavId(pathname);
  const page = pageTitles[activeId] ?? pageTitles.upload;

  const appClass = [
    "app",
    collapsed ? "app--sidebar-collapsed" : "",
    mobileOpen ? "app--sidebar-open" : "",
  ]
    .filter(Boolean)
    .join(" ");

  const isVisible = (item: NavItem) =>
    !item.hidden && !(item.requiresInvoice && !invoiceId) && Boolean(item.href || item.requiresInvoice);

  // Drop section labels whose items are all hidden, so no empty headings render.
  const visibleEntries = navStructure.filter((entry, index) => {
    if (!isSection(entry)) return isVisible(entry);
    const following = navStructure.slice(index + 1);
    const nextSection = following.findIndex(isSection);
    const items = (nextSection === -1 ? following : following.slice(0, nextSection)) as NavItem[];
    return items.some(isVisible);
  });

  const renderItem = (item: NavItem) => {
    const href = item.requiresInvoice && invoiceId ? `/invoices/${invoiceId}/review` : item.href;
    if (!href) return null;

    const className = `sidebar__item ${item.id === activeId ? "sidebar__item--active" : ""}`.trim();

    return (
      <Link
        key={item.id}
        href={href}
        className={className}
        onClick={() => setMobileOpen(false)}
      >
        <span className="sidebar__icon">
          <Icon name={item.icon} />
        </span>
        <span className="sidebar__item-text">{item.label}</span>
        {item.badge !== undefined && <span className="sidebar__badge">{item.badge}</span>}
      </Link>
    );
  };

  return (
    <div className={appClass}>
      <aside className="sidebar">
        <div className="sidebar__brand">
          <div className="sidebar__logo">
            <Icon name="zap" />
          </div>
          <div className="sidebar__brand-text">
            <span className="sidebar__brand-name">xChange</span>
            <span className="sidebar__brand-sub">Factuurconversie</span>
          </div>
        </div>

        <nav className="sidebar__nav">
          {visibleEntries.map((entry) =>
            isSection(entry) ? (
              <div className="sidebar__section-label" key={`section-${entry.section}`}>
                {entry.section}
              </div>
            ) : (
              renderItem(entry)
            ),
          )}
        </nav>

        <div className="sidebar__footer">
          <div className="sidebar__user">
            <div className="sidebar__avatar">{initials(user?.name)}</div>
            <div className="sidebar__user-text">
              <span className="sidebar__user-name">{user?.name ?? "Green IT Solutions"}</span>
              <span className="sidebar__user-role">{user ? roleLabel(user.role) : "Prototype"}</span>
            </div>
          </div>
          <div className="sidebar__footer-row">
            <button
              type="button"
              className="sidebar__toggle"
              onClick={() => setCollapsed((value) => !value)}
              aria-label="Zijbalk in- of uitklappen"
            >
              <Icon name="panelLeft" />
            </button>
            {user && (
              <button
                type="button"
                className="sidebar__toggle"
                onClick={() => void signOut()}
                aria-label="Uitloggen"
                title="Uitloggen"
              >
                <Icon name="x" />
              </button>
            )}
          </div>
          <div className="sidebar__powered">
            Aangedreven door
            <strong>Green IT Solutions</strong>
          </div>
        </div>
      </aside>

      <div
        className={`sidebar-overlay ${mobileOpen ? "sidebar-overlay--visible" : ""}`.trim()}
        onClick={() => setMobileOpen(false)}
      />

      <div className="app__main">
        <header className="header">
          <div className="header__left">
            <button
              type="button"
              className="header__menu-toggle"
              onClick={() => setMobileOpen(true)}
              aria-label="Menu openen"
            >
              <Icon name="menu" />
            </button>
            <div>
              <div className="header__title">{page.title}</div>
              <div className="header__breadcrumb">{page.breadcrumb}</div>
            </div>
          </div>
          <div className="header__right">
            <span className="header__date">Prototype — mockverwerking</span>
          </div>
        </header>

        <main className="content">
          <div className="content__inner">{children}</div>
        </main>
      </div>
    </div>
  );
}

/** Initials for the avatar, matching Bookkeeping's two-letter block. */
function initials(name?: string): string {
  if (!name) return "GS";
  const parts = name.trim().split(/\s+/);
  const letters = parts.length > 1 ? parts[0][0] + parts[parts.length - 1][0] : parts[0].slice(0, 2);
  return letters.toUpperCase();
}

function roleLabel(role: string): string {
  if (role === "Admin") return "Beheerder";
  if (role === "ReadOnly") return "Alleen lezen";
  return "Gebruiker";
}
