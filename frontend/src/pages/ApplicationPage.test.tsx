import { act, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApplicationPage } from './ApplicationPage';
import { api } from '../api/endpoints';
import { ApiError } from '../api/client';
import { STUDENT_ID_STORAGE_KEY } from '../hooks/useStudentId';
import { STUDENT, makeHold, renderWithProviders } from '../test/utils';

function renderPage() {
  return renderWithProviders(<ApplicationPage />, { route: '/holds/h_test', path: '/holds/:holdId' });
}

describe('ApplicationPage', () => {
  beforeEach(() => window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, STUDENT));
  afterEach(() => vi.useRealTimers());

  it('flips to EXPIRED without a reload and disables paying', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const hold = makeHold({ expiresAt: new Date(Date.now() + 2000).toISOString() });
    vi.spyOn(api, 'getHold').mockResolvedValue(hold);

    renderPage();
    const pay = await screen.findByRole('button', { name: 'ভর্তি ফি দিন' });
    expect(pay).toBeEnabled();
    expect(screen.getByTestId('status-badge')).toHaveAttribute('data-status', 'HELD');

    await act(() => vi.advanceTimersByTimeAsync(2500));

    expect(screen.getByTestId('status-badge')).toHaveTextContent('মেয়াদ শেষ');
    expect(screen.getByRole('button', { name: 'ভর্তি ফি দিন' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'বাতিল' })).toBeDisabled();
  });

  it('confirms on pay', async () => {
    let current = makeHold();
    vi.spyOn(api, 'getHold').mockImplementation(async () => current);
    const confirm = vi.spyOn(api, 'confirmHold').mockImplementation(async () => {
      current = { ...current, status: 'CONFIRMED', confirmedAt: current.createdAt };
      return current;
    });

    renderPage();
    await userEvent.click(await screen.findByRole('button', { name: 'ভর্তি ফি দিন' }));

    expect(await screen.findByText('ভর্তি নিশ্চিত হয়েছে ✅')).toBeInTheDocument();
    expect(confirm).toHaveBeenCalledWith('h_test', STUDENT);
    expect(screen.getByTestId('status-badge')).toHaveAttribute('data-status', 'CONFIRMED');
  });

  it('shows a readable message when paying fails', async () => {
    vi.spyOn(api, 'getHold').mockResolvedValue(makeHold());
    vi.spyOn(api, 'confirmHold').mockRejectedValue(new ApiError(409, 'HOLD_EXPIRED', 'expired'));

    renderPage();
    await userEvent.click(await screen.findByRole('button', { name: 'ভর্তি ফি দিন' }));
    expect(await screen.findByText('আবেদনের মেয়াদ শেষ হয়ে গেছে। চাইলে আবার আবেদন করুন।')).toBeInTheDocument();
  });

  it('asks before cancelling', async () => {
    vi.spyOn(api, 'getHold').mockResolvedValue(makeHold());
    const cancel = vi.spyOn(api, 'cancelHold').mockResolvedValue(undefined);

    renderPage();
    await userEvent.click(await screen.findByRole('button', { name: 'বাতিল' }));
    expect(cancel).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'হ্যাঁ, বাতিল করুন' }));
    expect(await screen.findByText('আবেদন বাতিল করা হয়েছে।')).toBeInTheDocument();
    expect(cancel).toHaveBeenCalledWith('h_test', STUDENT);
  });

  it('disables actions on an application that belongs to another student', async () => {
    vi.spyOn(api, 'getHold').mockResolvedValue(makeHold({ studentId: '99999999999999999' }));
    renderPage();
    expect(await screen.findByRole('button', { name: 'ভর্তি ফি দিন' })).toBeDisabled();
    expect(screen.getByText(/অন্য একটি জন্ম নিবন্ধন নম্বরের/)).toBeInTheDocument();
  });

  it('shows the network message when the server is down', async () => {
    vi.spyOn(api, 'getHold').mockRejectedValue(new ApiError(0, 'NETWORK_ERROR', 'down'));
    renderPage();
    expect(
      await screen.findByText('সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। একটু পরে আবার চেষ্টা করুন।'),
    ).toBeInTheDocument();
  });
});
