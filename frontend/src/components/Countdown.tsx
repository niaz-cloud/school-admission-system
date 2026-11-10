import { formatDuration } from '../lib/time';

/** Renders mm:ss; turns red in the final minute. */
export function Countdown({ remainingMs }: { remainingMs: number }) {
  const urgent = remainingMs > 0 && remainingMs <= 60_000;
  return (
    <span className={`countdown mono${urgent ? ' countdown--urgent' : ''}`} data-testid="countdown" aria-live="off">
      {formatDuration(remainingMs)}
    </span>
  );
}
