import { useEffect, useState } from 'react';
import { remainingMs as computeRemaining, serverNow } from '../lib/time';

const TICK_MS = 250;

/**
 * Live time left until `expiresAt`, re-rendering several times a second, measured against the
 * server's clock. Pass null to disable. `expired` flips the moment the time runs out, with no
 * network call and no page reload (requirement U2).
 */
export function useCountdown(expiresAt: string | null | undefined): { remainingMs: number; expired: boolean } {
  const [now, setNow] = useState(() => serverNow());

  useEffect(() => {
    if (!expiresAt) return undefined;
    setNow(serverNow());
    const id = window.setInterval(() => {
      const current = serverNow();
      setNow(current);
      if (computeRemaining(expiresAt, current) <= 0) window.clearInterval(id);
    }, TICK_MS);
    return () => window.clearInterval(id);
  }, [expiresAt]);

  if (!expiresAt) return { remainingMs: 0, expired: false };
  const left = computeRemaining(expiresAt, now);
  return { remainingMs: left, expired: left <= 0 };
}
