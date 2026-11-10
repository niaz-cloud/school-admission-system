import { useCallback, useSyncExternalStore } from 'react';
import { isValidStudentId } from '../lib/studentId';

export const STUDENT_ID_STORAGE_KEY = 'school-admission.studentId';

const listeners = new Set<() => void>();

function read(): string | null {
  try {
    const value = window.localStorage.getItem(STUDENT_ID_STORAGE_KEY);
    return value && isValidStudentId(value) ? value : null;
  } catch {
    return null; // storage blocked (private mode etc.)
  }
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  // Keep several open tabs in sync.
  const onStorage = (event: StorageEvent) => {
    if (event.key === STUDENT_ID_STORAGE_KEY) listener();
  };
  window.addEventListener('storage', onStorage);
  return () => {
    listeners.delete(listener);
    window.removeEventListener('storage', onStorage);
  };
}

export function setStoredStudentId(value: string | null): void {
  try {
    if (value) window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, value);
    else window.localStorage.removeItem(STUDENT_ID_STORAGE_KEY);
  } catch {
    // Ignore: the ID then lasts only for this page view.
  }
  listeners.forEach((listener) => listener());
}

/**
 * The student's 17-digit birth registration number, persisted in localStorage so it survives
 * page refreshes and browser restarts (requirement U4).
 */
export function useStudentId(): [string | null, (value: string | null) => void] {
  const studentId = useSyncExternalStore(subscribe, read, () => null);
  const update = useCallback((value: string | null) => setStoredStudentId(value), []);
  return [studentId, update];
}
