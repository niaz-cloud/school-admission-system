import type { ReactNode } from 'react';
import { errorMessage } from '../lib/errors';

interface Props {
  error?: unknown;
  message?: string;
  onRetry?: () => void;
  variant?: 'error' | 'warning';
  children?: ReactNode;
}

/** A visible, human-readable error. Never leave a failure only in the console. */
export function ErrorAlert({ error, message, onRetry, variant = 'error', children }: Props) {
  const text = message ?? errorMessage(error);
  return (
    <div className={`alert alert--${variant}`} role="alert">
      <span>{text}</span>
      {children}
      {onRetry && (
        <button type="button" className="btn btn--small" onClick={onRetry}>
          আবার চেষ্টা করুন
        </button>
      )}
    </div>
  );
}
