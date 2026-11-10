import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { StudentIdBar } from './StudentIdBar';
import { STUDENT_ID_STORAGE_KEY } from '../hooks/useStudentId';
import { renderWithProviders } from '../test/utils';

const LABEL = 'শিক্ষার্থীর জন্ম নিবন্ধন নম্বর';

describe('StudentIdBar', () => {
  it('rejects 16 digits', async () => {
    renderWithProviders(<StudentIdBar />);
    await userEvent.type(screen.getByLabelText(LABEL), '1234567890123456');
    await userEvent.click(screen.getByRole('button', { name: 'সংরক্ষণ' }));
    expect(screen.getByRole('alert')).toHaveTextContent('১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন');
    expect(window.localStorage.getItem(STUDENT_ID_STORAGE_KEY)).toBeNull();
  });

  it('accepts 17 digits and persists them', async () => {
    renderWithProviders(<StudentIdBar />);
    await userEvent.type(screen.getByLabelText(LABEL), '20152691234567890');
    await userEvent.click(screen.getByRole('button', { name: 'সংরক্ষণ' }));
    expect(screen.getByTestId('current-student-id')).toHaveTextContent('20152691234567890');
    expect(window.localStorage.getItem(STUDENT_ID_STORAGE_KEY)).toBe('20152691234567890');
  });

  it('converts Bengali digits', async () => {
    renderWithProviders(<StudentIdBar />);
    await userEvent.type(screen.getByLabelText(LABEL), '২০১৫২৬৯১২৩৪৫৬৭৮৯০');
    await userEvent.click(screen.getByRole('button', { name: 'সংরক্ষণ' }));
    expect(screen.getByTestId('current-student-id')).toHaveTextContent('20152691234567890');
  });

  it('restores a saved ID after a reload', () => {
    window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, '20152691234567890');
    renderWithProviders(<StudentIdBar />);
    expect(screen.getByTestId('current-student-id')).toHaveTextContent('20152691234567890');
  });
});
