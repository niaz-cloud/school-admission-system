import { useState, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useStudentId } from '../hooks/useStudentId';
import { isValidStudentId, normalizeStudentId } from '../lib/studentId';
import { queryKeys } from '../api/endpoints';

/** The student's identity: a 17-digit birth registration number, kept in localStorage. */
export function StudentIdBar() {
  const [studentId, setStudentId] = useStudentId();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState('');
  const [error, setError] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const showForm = editing || !studentId;

  function startEditing() {
    setDraft(studentId ?? '');
    setError(null);
    setEditing(true);
  }

  function submit(event: FormEvent) {
    event.preventDefault();
    const normalized = normalizeStudentId(draft);
    if (!isValidStudentId(normalized)) {
      setError('১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন');
      return;
    }
    setStudentId(normalized);
    setEditing(false);
    setError(null);
    void queryClient.invalidateQueries({ queryKey: queryKeys.myHoldsAll });
  }

  if (!showForm) {
    return (
      <div className="student-bar student-bar--set" aria-live="polite">
        <span>
          শিক্ষার্থী: <strong className="mono" data-testid="current-student-id">{studentId}</strong>
        </span>
        <button type="button" className="btn btn--small btn--ghost" onClick={startEditing}>
          পরিবর্তন
        </button>
      </div>
    );
  }

  const digitsTyped = normalizeStudentId(draft).length;

  return (
    <form className="student-bar" onSubmit={submit} noValidate>
      <label htmlFor="student-id-input" className="student-bar__label">
        শিক্ষার্থীর জন্ম নিবন্ধন নম্বর
      </label>
      <div className="student-bar__row">
        <input
          id="student-id-input"
          className={`input mono${error ? ' input--invalid' : ''}`}
          inputMode="numeric"
          autoComplete="off"
          placeholder="১৭ অঙ্কের নম্বর, যেমন 20152691234567890"
          value={draft}
          maxLength={30}
          onChange={(event) => {
            setDraft(event.target.value);
            setError(null);
          }}
          aria-invalid={error ? true : undefined}
          aria-describedby="student-id-help"
        />
        <button type="submit" className="btn btn--primary">
          সংরক্ষণ
        </button>
        {studentId && (
          <button type="button" className="btn btn--ghost" onClick={() => setEditing(false)}>
            বাতিল
          </button>
        )}
      </div>
      <div id="student-id-help" className={error ? 'field-error' : 'field-help'} role={error ? 'alert' : undefined}>
        {error ?? `${digitsTyped}/17 অঙ্ক`}
      </div>
    </form>
  );
}
