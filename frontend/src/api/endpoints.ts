import { recordServerTime } from '../lib/time';
import { request } from './client';
import type { ClassSummary, CreateClassInput, Hold } from './types';

function withServerTime<T extends { serverTime?: string }>(value: T): T {
  if (value?.serverTime) recordServerTime(value.serverTime);
  return value;
}

export const api = {
  createClass: (input: CreateClassInput) => request<ClassSummary>('/classes', { method: 'POST', body: input }),

  listClasses: () => request<ClassSummary[]>('/classes'),

  getClass: (classId: string) => request<ClassSummary>(`/classes/${encodeURIComponent(classId)}`),

  createHold: async (classId: string, studentId: string) =>
    withServerTime(
      await request<Hold>(`/classes/${encodeURIComponent(classId)}/holds`, { method: 'POST', studentId }),
    ),

  getHold: async (holdId: string) => withServerTime(await request<Hold>(`/holds/${encodeURIComponent(holdId)}`)),

  confirmHold: async (holdId: string, studentId: string) =>
    withServerTime(
      await request<Hold>(`/holds/${encodeURIComponent(holdId)}/confirm`, { method: 'POST', studentId }),
    ),

  cancelHold: (holdId: string, studentId: string) =>
    request<void>(`/holds/${encodeURIComponent(holdId)}`, { method: 'DELETE', studentId }),

  myHolds: async (studentId: string) => {
    const holds = await request<Hold[]>('/me/holds', { studentId });
    if (holds.length > 0) recordServerTime(holds[0].serverTime);
    return holds;
  },
};

export const queryKeys = {
  classes: ['classes'] as const,
  hold: (holdId: string) => ['hold', holdId] as const,
  myHoldsAll: ['me-holds'] as const,
  myHolds: (studentId: string | null) => ['me-holds', studentId] as const,
};
