import type { ReactElement } from 'react';
import { render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ToastProvider } from '../components/Toast';
import type { Hold } from '../api/types';

export function renderWithProviders(ui: ReactElement, { route = '/', path = '*' }: { route?: string; path?: string } = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter initialEntries={[route]}>
          <Routes>
            <Route path={path} element={ui} />
            <Route path="*" element={<div data-testid="navigated-elsewhere" />} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

export const STUDENT = '20152691234567890';

export function makeHold(overrides: Partial<Hold> = {}): Hold {
  const now = new Date(Date.now()).toISOString();
  return {
    holdId: 'h_test',
    classId: 'c_test',
    className: 'Class 6 - Morning Shift',
    studentId: STUDENT,
    status: 'HELD',
    expiresAt: new Date(Date.now() + 600_000).toISOString(),
    createdAt: now,
    confirmedAt: null,
    cancelledAt: null,
    serverTime: now,
    ...overrides,
  };
}
