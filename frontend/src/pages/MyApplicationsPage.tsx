import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api, queryKeys } from '../api/endpoints';
import type { Hold, HoldStatus } from '../api/types';
import { Countdown } from '../components/Countdown';
import { ErrorAlert } from '../components/ErrorAlert';
import { StatusBadge } from '../components/StatusBadge';
import { useCountdown } from '../hooks/useCountdown';
import { useStudentId } from '../hooks/useStudentId';
import { formatDateTime } from '../lib/time';

export function MyApplicationsPage() {
  const [studentId] = useStudentId();

  const holds = useQuery({
    queryKey: queryKeys.myHolds(studentId),
    queryFn: () => api.myHolds(studentId!),
    enabled: !!studentId,
    refetchInterval: 5000,
  });

  if (!studentId) {
    return (
      <section className="card">
        <h1>আমার আবেদন</h1>
        <div className="alert alert--info">আপনার আবেদনগুলো দেখতে উপরে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।</div>
      </section>
    );
  }

  return (
    <section>
      <div className="page-title">
        <h1>আমার আবেদন</h1>
      </div>

      {holds.isPending && <p className="muted" role="status">লোড হচ্ছে…</p>}

      {holds.isError && !holds.data && <ErrorAlert error={holds.error} onRetry={() => void holds.refetch()} />}
      {holds.isError && holds.data && (
        <ErrorAlert variant="warning" message="সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। শেষ পাওয়া তথ্য দেখানো হচ্ছে।" />
      )}

      {holds.data && holds.data.length === 0 && (
        <div className="card empty">
          <p>এখনো কোনো আবেদন নেই।</p>
          <Link to="/" className="btn btn--primary">
            শ্রেণি তালিকা দেখুন
          </Link>
        </div>
      )}

      {holds.data && holds.data.length > 0 && (
        <ul className="hold-list">
          {holds.data.map((hold) => (
            <HoldItem key={hold.holdId} hold={hold} />
          ))}
        </ul>
      )}
    </section>
  );
}

function HoldItem({ hold }: { hold: Hold }) {
  const { remainingMs, expired } = useCountdown(hold.status === 'HELD' ? hold.expiresAt : null);
  const status: HoldStatus = hold.status === 'HELD' && expired ? 'EXPIRED' : hold.status;

  return (
    <li className="hold-item card" data-testid={`hold-item-${hold.holdId}`}>
      <div className="hold-item__main">
        <strong>{hold.className}</strong>
        <StatusBadge status={status} />
      </div>
      <div className="hold-item__meta small muted">
        আবেদন: {formatDateTime(hold.createdAt)}
        {status === 'HELD' && (
          <>
            {' · '}সময় বাকি <Countdown remainingMs={remainingMs} />
          </>
        )}
      </div>
      <Link to={`/holds/${hold.holdId}`} className="btn btn--secondary btn--small">
        {status === 'HELD' ? 'ফি দিন / বিস্তারিত' : 'বিস্তারিত'}
      </Link>
    </li>
  );
}
