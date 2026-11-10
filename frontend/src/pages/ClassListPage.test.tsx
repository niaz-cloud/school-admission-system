import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ClassListPage } from './ClassListPage';
import { api } from '../api/endpoints';
import { ApiError } from '../api/client';
import type { ClassSummary } from '../api/types';
import { STUDENT_ID_STORAGE_KEY } from '../hooks/useStudentId';
import { STUDENT, makeHold, renderWithProviders } from '../test/utils';

const CLASS: ClassSummary = {
  id: 'c_1',
  name: 'Class 6 - Morning Shift',
  totalSeats: 120,
  heldSeats: 15,
  confirmedSeats: 80,
  availableSeats: 25,
};

describe('ClassListPage', () => {
  it('renders seat counts', async () => {
    vi.spyOn(api, 'listClasses').mockResolvedValue([CLASS]);
    renderWithProviders(<ClassListPage />);
    const row = await screen.findByTestId('class-row-c_1');
    expect(row).toHaveTextContent('Class 6 - Morning Shift');
    for (const value of ['120', '15', '80', '25']) expect(row).toHaveTextContent(value);
  });

  it('disables apply without a student ID', async () => {
    vi.spyOn(api, 'listClasses').mockResolvedValue([CLASS]);
    renderWithProviders(<ClassListPage />);
    expect(await screen.findByRole('button', { name: 'আবেদন করুন' })).toBeDisabled();
  });

  it('shows the SEATS_FULL message on 409', async () => {
    window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, STUDENT);
    vi.spyOn(api, 'listClasses').mockResolvedValue([CLASS]);
    vi.spyOn(api, 'myHolds').mockResolvedValue([]);
    vi.spyOn(api, 'createHold').mockRejectedValue(new ApiError(409, 'SEATS_FULL', 'full'));

    renderWithProviders(<ClassListPage />);
    await userEvent.click(await screen.findByRole('button', { name: 'আবেদন করুন' }));
    expect(await screen.findByText('দুঃখিত, এই class-এ আর কোনো seat খালি নেই।')).toBeInTheDocument();
  });

  it('links to an existing application instead of offering apply', async () => {
    window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, STUDENT);
    vi.spyOn(api, 'listClasses').mockResolvedValue([CLASS]);
    vi.spyOn(api, 'myHolds').mockResolvedValue([makeHold({ classId: 'c_1', holdId: 'h_1' })]);
    renderWithProviders(<ClassListPage />);
    expect(await screen.findByRole('link', { name: 'আবেদন দেখুন' })).toHaveAttribute('href', '/holds/h_1');
  });

  it('shows "no seats" when the class is full', async () => {
    window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, STUDENT);
    vi.spyOn(api, 'listClasses').mockResolvedValue([{ ...CLASS, availableSeats: 0 }]);
    vi.spyOn(api, 'myHolds').mockResolvedValue([]);
    renderWithProviders(<ClassListPage />);
    expect(await screen.findByRole('button', { name: 'seat নেই' })).toBeDisabled();
  });

  it('shows a readable error with retry when the server is down', async () => {
    vi.spyOn(api, 'listClasses').mockRejectedValue(new ApiError(0, 'NETWORK_ERROR', 'down'));
    renderWithProviders(<ClassListPage />);
    expect(
      await screen.findByText('সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। একটু পরে আবার চেষ্টা করুন।'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'আবার চেষ্টা করুন' })).toBeInTheDocument();
  });
});
