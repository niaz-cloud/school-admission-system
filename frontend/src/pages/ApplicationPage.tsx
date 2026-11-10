import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../api/client';
import { api, queryKeys } from '../api/endpoints';
import type { Hold, HoldStatus } from '../api/types';
import { Countdown } from '../components/Countdown';
import { ErrorAlert } from '../components/ErrorAlert';
import { StatusBadge } from '../components/StatusBadge';
import { useCountdown } from '../hooks/useCountdown';
import { useStudentId } from '../hooks/useStudentId';
import { errorMessage } from '../lib/errors';
import { formatDateTime } from '../lib/time';

export const HOLD_POLL_MS = 5000;

export function ApplicationPage() {
  const { holdId = '' } = useParams();
  const [studentId] = useStudentId();
  const queryClient = useQueryClient();
  const [confirmingCancel, setConfirmingCancel] = useState(false);
  const [notice, setNotice] = useState<{ kind: 'success' | 'error'; text: string } | null>(null);

  const holdQuery = useQuery({
    queryKey: queryKeys.hold(holdId),
    queryFn: () => api.getHold(holdId),
    refetchInterval: HOLD_POLL_MS,
    enabled: holdId.length > 0,
  });
  const hold = holdQuery.data;

  const { remainingMs, expired } = useCountdown(hold?.status === 'HELD' ? hold.expiresAt : null);
  // Flip to EXPIRED the instant the countdown hits zero, without waiting for the server.
  const status: HoldStatus | undefined = hold?.status === 'HELD' && expired ? 'EXPIRED' : hold?.status;

  function refreshEverything() {
    void queryClient.invalidateQueries({ queryKey: queryKeys.hold(holdId) });
    void queryClient.invalidateQueries({ queryKey: queryKeys.classes });
    void queryClient.invalidateQueries({ queryKey: queryKeys.myHoldsAll });
  }

  // Once the local countdown expires, re-sync with the server a single time.
  const syncedExpiry = useRef<string | null>(null);
  useEffect(() => {
    if (hold?.status === 'HELD' && expired && syncedExpiry.current !== hold.holdId) {
      syncedExpiry.current = hold.holdId;
      refreshEverything();
    }
  }, [expired, hold?.status, hold?.holdId]); // refreshEverything only touches stable values

  const confirm = useMutation({
    mutationFn: () => api.confirmHold(holdId, studentId!),
    onMutate: () => setNotice(null),
    onSuccess: (updated) => {
      queryClient.setQueryData<Hold>(queryKeys.hold(holdId), updated);
      setNotice({ kind: 'success', text: 'ভর্তি নিশ্চিত হয়েছে ✅' });
      refreshEverything();
    },
    onError: (error) => {
      setNotice({ kind: 'error', text: errorMessage(error) });
      refreshEverything();
    },
  });

  const cancel = useMutation({
    mutationFn: () => api.cancelHold(holdId, studentId!),
    onMutate: () => setNotice(null),
    onSuccess: () => {
      queryClient.setQueryData<Hold>(queryKeys.hold(holdId), (old) =>
        old ? { ...old, status: 'CANCELLED', cancelledAt: new Date().toISOString() } : old,
      );
      setConfirmingCancel(false);
      setNotice({ kind: 'success', text: 'আবেদন বাতিল করা হয়েছে।' });
      refreshEverything();
    },
    onError: (error) => {
      setConfirmingCancel(false);
      setNotice({ kind: 'error', text: errorMessage(error) });
      refreshEverything();
    },
  });

  if (holdQuery.isPending) {
    return <p className="muted" role="status">লোড হচ্ছে…</p>;
  }

  if (!hold) {
    const notFound = holdQuery.error instanceof ApiError && holdQuery.error.code === 'HOLD_NOT_FOUND';
    return (
      <div className="card">
        <ErrorAlert error={holdQuery.error} onRetry={notFound ? undefined : () => void holdQuery.refetch()} />
        <Link to="/me">আমার আবেদনে ফিরে যান</Link>
      </div>
    );
  }

  const isOwner = studentId === hold.studentId;
  const busy = confirm.isPending || cancel.isPending;
  const canAct = status === 'HELD' && isOwner && !busy;

  return (
    <section className="card application">
      <div className="page-title">
        <h1>{hold.className}</h1>
        {status && <StatusBadge status={status} />}
      </div>

      {holdQuery.isError && (
        <ErrorAlert variant="warning" message="সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। শেষ পাওয়া তথ্য দেখানো হচ্ছে।" />
      )}

      {!studentId && (
        <div className="alert alert--info">ফি দিতে বা বাতিল করতে উপরে আপনার জন্ম নিবন্ধন নম্বর দিন।</div>
      )}
      {studentId && !isOwner && (
        <div className="alert alert--warning" role="alert">
          এই আবেদনটি অন্য একটি জন্ম নিবন্ধন নম্বরের। আপনি শুধু দেখতে পারবেন, ফি দেওয়া বা বাতিল করা যাবে না।
        </div>
      )}

      {notice && (
        <div className={`alert alert--${notice.kind}`} role={notice.kind === 'error' ? 'alert' : 'status'}>
          {notice.text}
        </div>
      )}

      {status === 'HELD' && (
        <div className="countdown-box">
          <span>সময় বাকি</span>
          <Countdown remainingMs={remainingMs} />
          <span className="muted small">এই সময়ের মধ্যে ফি না দিলে seat ছেড়ে দেওয়া হবে।</span>
        </div>
      )}
      {status === 'EXPIRED' && (
        <div className="alert alert--error">
          আবেদনের মেয়াদ শেষ হয়ে গেছে। seat ছেড়ে দেওয়া হয়েছে। চাইলে <Link to="/">আবার আবেদন করুন</Link>।
        </div>
      )}
      {status === 'CONFIRMED' && <div className="alert alert--success">আপনার ভর্তি নিশ্চিত হয়েছে।</div>}

      <dl className="details">
        <dt>আবেদন নম্বর</dt>
        <dd className="mono">{hold.holdId}</dd>
        <dt>শিক্ষার্থী</dt>
        <dd className="mono">{hold.studentId}</dd>
        <dt>আবেদনের সময়</dt>
        <dd>{formatDateTime(hold.createdAt)}</dd>
        <dt>মেয়াদ শেষ</dt>
        <dd>{formatDateTime(hold.expiresAt)}</dd>
        {hold.confirmedAt && (
          <>
            <dt>নিশ্চিত হয়েছে</dt>
            <dd>{formatDateTime(hold.confirmedAt)}</dd>
          </>
        )}
      </dl>

      <div className="button-row">
        <button type="button" className="btn btn--primary btn--large" disabled={!canAct} onClick={() => confirm.mutate()}>
          {confirm.isPending ? 'অপেক্ষা করুন…' : 'ভর্তি ফি দিন'}
        </button>

        {!confirmingCancel ? (
          <button
            type="button"
            className="btn btn--danger-outline"
            disabled={!canAct}
            onClick={() => setConfirmingCancel(true)}
          >
            বাতিল
          </button>
        ) : (
          <span className="inline-confirm" role="group" aria-label="বাতিল নিশ্চিত করুন">
            <span>নিশ্চিত?</span>
            <button type="button" className="btn btn--danger" disabled={!canAct} onClick={() => cancel.mutate()}>
              {cancel.isPending ? 'বাতিল হচ্ছে…' : 'হ্যাঁ, বাতিল করুন'}
            </button>
            <button type="button" className="btn btn--ghost" disabled={busy} onClick={() => setConfirmingCancel(false)}>
              না
            </button>
          </span>
        )}
      </div>

      <p className="small">
        <Link to="/me">← আমার সব আবেদন</Link> · <Link to="/">শ্রেণি তালিকা</Link>
      </p>
    </section>
  );
}
