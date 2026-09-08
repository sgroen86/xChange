"use client";

/**
 * Toasts — same markup and behaviour as Bookkeeping's js/components/toast.js
 * (bottom-right stack, 3500ms auto-dismiss, semantic left border).
 * Exposed as a context because React owns the DOM here.
 */

import { createContext, useCallback, useContext, useMemo, useState } from "react";
import { Icon } from "./Icon";

export type ToastType = "success" | "danger" | "warning" | "info";

interface ToastRecord {
  id: number;
  message: string;
  type: ToastType;
}

const ToastContext = createContext<(message: string, type?: ToastType, duration?: number) => void>(
  () => {},
);

const TOAST_ICON: Record<ToastType, string> = {
  success: "check",
  danger: "x",
  warning: "info",
  info: "info",
};

let nextId = 1;

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<ToastRecord[]>([]);

  const showToast = useCallback((message: string, type: ToastType = "success", duration = 3500) => {
    const id = nextId++;
    setToasts((current) => [...current, { id, message, type }]);
    window.setTimeout(() => {
      setToasts((current) => current.filter((toast) => toast.id !== id));
    }, duration);
  }, []);

  const value = useMemo(() => showToast, [showToast]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="toast-container">
        {toasts.map((toast) => (
          <div key={toast.id} className={`toast toast--${toast.type}`} role="status">
            <Icon name={TOAST_ICON[toast.type]} className="toast__icon" />
            <span>{toast.message}</span>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  return useContext(ToastContext);
}
