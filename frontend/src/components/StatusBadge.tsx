import type { HoldStatus } from '../api/types';
import { STATUS_LABELS } from '../lib/errors';

export function StatusBadge({ status }: { status: HoldStatus }) {
  return (
    <span className={`badge badge--${status.toLowerCase()}`} data-testid="status-badge" data-status={status}>
      {STATUS_LABELS[status]}
    </span>
  );
}
