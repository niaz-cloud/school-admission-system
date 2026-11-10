import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../api/client';
import { api, queryKeys } from '../api/endpoints';
import type { ClassSummary, Hold } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { useStudentId } from '../hooks/useStudentId';
import { errorMessage } from '../lib/errors';
import { formatClock, serverNow } from '../lib/time';

export const CLASS_LIST_POLL_MS = 3000;

/** classId → holdId of this student's HELD (not yet expired) or CONFIRMED application. */
function activeHoldsByClass(holds: Hold[] | undefined): Map<string, string> {
  const map = new Map<string, string>();
  const now = serverNow();
  for (const hold of holds ?? []) {
    const active = hold.status === 'CONFIRMED' || (hold.status === 'HELD' && Date.parse(hold.expiresAt) > now);
    if (active && !map.has(hold.classId)) map.set(hold.classId, hold.holdId);
  }
  return map;
}

export function ClassListPage() {
  const [studentId] = useStudentId();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [actionError, setActionError] = useState<{ classId: string; message: string } | null>(null);

  const classes = useQuery({
    queryKey: queryKeys.classes,
    queryFn: api.listClasses,
    refetchInterval: CLASS_LIST_POLL_MS,
  });

  const myHolds = useQuery({
    queryKey: queryKeys.myHolds(studentId),
    queryFn: () => api.myHolds(studentId!),
    enabled: !!studentId,
    refetchInterval: 5000,
  });

  const activeByClass = useMemo(() => activeHoldsByClass(myHolds.data), [myHolds.data]);

  const apply = useMutation({
    mutationFn: (classId: string) => api.createHold(classId, studentId!),
    onMutate: () => setActionError(null),
    onSuccess: (hold) => {
      queryClient.setQueryData(queryKeys.hold(hold.holdId), hold);
      void queryClient.invalidateQueries({ queryKey: queryKeys.classes });
      void queryClient.invalidateQueries({ queryKey: queryKeys.myHoldsAll });
      navigate(`/holds/${hold.holdId}`);
    },
    onError: (error, classId) => {
      const existing = error instanceof ApiError ? error.extra('holdId') : undefined;
      if (error instanceof ApiError && error.code === 'ALREADY_APPLIED' && typeof existing === 'string') {
        navigate(`/holds/${existing}`);
        return;
      }
      setActionError({ classId, message: errorMessage(error) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.classes });
    },
  });

  if (classes.isPending) {
    return <p className="muted" role="status">লোড হচ্ছে…</p>;
  }

  if (classes.isError && !classes.data) {
    return <ErrorAlert error={classes.error} onRetry={() => void classes.refetch()} />;
  }

  const list = classes.data ?? [];

  return (
    <section>
      <div className="page-title">
        <h1>শ্রেণি তালিকা</h1>
        <span className="muted small" data-testid="last-updated">
          সর্বশেষ আপডেট: {formatClock(classes.dataUpdatedAt)}
        </span>
      </div>

      {classes.isError && (
        <ErrorAlert
          variant="warning"
          message="সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। শেষ পাওয়া তথ্য দেখানো হচ্ছে।"
        />
      )}

      {!studentId && (
        <div className="alert alert--info">আবেদন করতে উপরে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।</div>
      )}

      {actionError && <ErrorAlert message={actionError.message} />}

      {list.length === 0 ? (
        <div className="card empty">
          <p>এখনো কোনো class তৈরি হয়নি।</p>
          <Link to="/admin/classes/new" className="btn btn--primary">
            নতুন class তৈরি করুন
          </Link>
        </div>
      ) : (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>নাম</th>
                <th className="num">মোট</th>
                <th className="num">hold-এ</th>
                <th className="num">নিশ্চিত</th>
                <th className="num">খালি</th>
                <th aria-label="কাজ" />
              </tr>
            </thead>
            <tbody>
              {list.map((item) => (
                <ClassRow
                  key={item.id}
                  item={item}
                  studentId={studentId}
                  activeHoldId={activeByClass.get(item.id)}
                  pending={apply.isPending && apply.variables === item.id}
                  anyPending={apply.isPending}
                  onApply={() => apply.mutate(item.id)}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

interface RowProps {
  item: ClassSummary;
  studentId: string | null;
  activeHoldId: string | undefined;
  pending: boolean;
  anyPending: boolean;
  onApply: () => void;
}

function ClassRow({ item, studentId, activeHoldId, pending, anyPending, onApply }: RowProps) {
  let action;
  if (activeHoldId) {
    action = (
      <Link to={`/holds/${activeHoldId}`} className="btn btn--secondary">
        আবেদন দেখুন
      </Link>
    );
  } else if (!studentId) {
    action = (
      <button type="button" className="btn btn--primary" disabled title="আগে জন্ম নিবন্ধন নম্বর দিন">
        আবেদন করুন
      </button>
    );
  } else if (item.availableSeats <= 0) {
    action = (
      <button type="button" className="btn" disabled>
        seat নেই
      </button>
    );
  } else {
    action = (
      <button type="button" className="btn btn--primary" onClick={onApply} disabled={anyPending}>
        {pending ? 'অপেক্ষা করুন…' : 'আবেদন করুন'}
      </button>
    );
  }

  return (
    <tr data-testid={`class-row-${item.id}`}>
      <td>{item.name}</td>
      <td className="num" data-label="মোট">{item.totalSeats}</td>
      <td className="num" data-label="hold-এ">{item.heldSeats}</td>
      <td className="num" data-label="নিশ্চিত">{item.confirmedSeats}</td>
      <td className="num" data-label="খালি">
        <strong className={item.availableSeats > 0 ? 'text-ok' : 'text-bad'}>{item.availableSeats}</strong>
      </td>
      <td className="actions">{action}</td>
    </tr>
  );
}
