export type HoldStatus = 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED';

export interface ClassSummary {
  id: string;
  name: string;
  totalSeats: number;
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
  createdAt?: string;
}

export interface Hold {
  holdId: string;
  classId: string;
  className: string;
  studentId: string;
  status: HoldStatus;
  expiresAt: string;
  createdAt: string;
  confirmedAt: string | null;
  cancelledAt: string | null;
  serverTime: string;
}

export interface CreateClassInput {
  name: string;
  totalSeats: number;
}

/** RFC 7807 body returned by the backend for every non-2xx response. */
export interface ProblemBody {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  [extra: string]: unknown;
}
