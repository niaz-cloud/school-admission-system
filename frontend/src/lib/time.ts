/**
 * The countdown must agree with the server, not with a possibly wrong client clock. Every hold
 * response carries serverTime; we keep the difference and read "now" as client time + offset.
 */
let serverOffsetMs = 0;

export function recordServerTime(serverTime: string, clientNow: number = Date.now()): void {
  const parsed = Date.parse(serverTime);
  if (!Number.isNaN(parsed)) serverOffsetMs = parsed - clientNow;
}

export function serverNow(): number {
  return Date.now() + serverOffsetMs;
}

/** For tests. */
export function resetServerOffset(): void {
  serverOffsetMs = 0;
}

export function remainingMs(expiresAt: string, now: number = serverNow()): number {
  const target = Date.parse(expiresAt);
  return Number.isNaN(target) ? 0 : Math.max(0, target - now);
}

/** 125_000 → "02:05"; an hour or more → "1:02:05". */
export function formatDuration(ms: number): string {
  const totalSeconds = Math.ceil(Math.max(0, ms) / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const mm = String(minutes).padStart(2, '0');
  const ss = String(seconds).padStart(2, '0');
  return hours > 0 ? `${hours}:${mm}:${ss}` : `${mm}:${ss}`;
}

export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleString('bn-BD', { dateStyle: 'medium', timeStyle: 'medium' });
}

export function formatClock(epochMs: number): string {
  return new Date(epochMs).toLocaleTimeString('bn-BD');
}
