import { act, renderHook } from '@testing-library/react';
import { useCountdown } from './useCountdown';

describe('useCountdown', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-01T04:00:00.000Z'));
  });
  afterEach(() => vi.useRealTimers());

  it('counts down and reports expired at zero', () => {
    const { result } = renderHook(() => useCountdown('2026-11-01T04:00:03.000Z'));
    expect(result.current.expired).toBe(false);
    expect(result.current.remainingMs).toBe(3000);

    act(() => vi.advanceTimersByTime(2000));
    expect(result.current.expired).toBe(false);
    expect(result.current.remainingMs).toBeLessThanOrEqual(1000);

    act(() => vi.advanceTimersByTime(1250));
    expect(result.current.expired).toBe(true);
    expect(result.current.remainingMs).toBe(0);
  });

  it('is inert when disabled', () => {
    const { result } = renderHook(() => useCountdown(null));
    expect(result.current).toEqual({ remainingMs: 0, expired: false });
  });
});
