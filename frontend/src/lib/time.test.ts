import { formatDuration, recordServerTime, remainingMs, serverNow } from './time';

describe('time helpers', () => {
  it('formats durations', () => {
    expect(formatDuration(0)).toBe('00:00');
    expect(formatDuration(125_000)).toBe('02:05');
    expect(formatDuration(600_000)).toBe('10:00');
    expect(formatDuration(3_725_000)).toBe('1:02:05');
    expect(formatDuration(-5)).toBe('00:00');
  });

  it('corrects for client clock skew using serverTime', () => {
    const clientNow = Date.now();
    recordServerTime(new Date(clientNow + 60_000).toISOString(), clientNow);
    expect(serverNow() - Date.now()).toBeGreaterThanOrEqual(59_000);
    expect(remainingMs(new Date(clientNow + 90_000).toISOString())).toBeLessThanOrEqual(30_000);
  });
});
