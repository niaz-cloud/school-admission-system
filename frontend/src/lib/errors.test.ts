import { ApiError } from '../api/client';
import { ERROR_MESSAGES, FALLBACK_MESSAGE, STATUS_LABELS, errorMessage } from './errors';

const CONTRACT_CODES = [
  'VALIDATION_ERROR',
  'CLASS_NOT_FOUND',
  'HOLD_NOT_FOUND',
  'STUDENT_ID_REQUIRED',
  'STUDENT_ID_INVALID',
  'SEATS_FULL',
  'ALREADY_APPLIED',
  'NOT_HOLD_OWNER',
  'HOLD_EXPIRED',
  'HOLD_CANCELLED',
  'HOLD_ALREADY_CONFIRMED',
  'NETWORK_ERROR',
  'INTERNAL_ERROR',
];

describe('errorMessage', () => {
  it.each(CONTRACT_CODES)('has a non-empty Bengali message for %s', (code) => {
    expect(ERROR_MESSAGES[code]).toBeTruthy();
    expect(errorMessage(new ApiError(409, code, 'detail'))).toContain(ERROR_MESSAGES[code]);
  });

  it('falls back for unknown codes and non-API errors', () => {
    expect(errorMessage(new ApiError(418, 'TEAPOT', 'x'))).toBe(FALLBACK_MESSAGE);
    expect(errorMessage(new Error('boom'))).toBe(FALLBACK_MESSAGE);
    expect(errorMessage(undefined)).toBe(FALLBACK_MESSAGE);
  });

  it('includes the server detail for validation errors', () => {
    expect(errorMessage(new ApiError(400, 'VALIDATION_ERROR', 'name is required.'))).toBe(
      'দেওয়া তথ্য সঠিক নয়: name is required.',
    );
  });

  it('labels every status', () => {
    expect(Object.keys(STATUS_LABELS).sort()).toEqual(['CANCELLED', 'CONFIRMED', 'EXPIRED', 'HELD']);
  });
});
