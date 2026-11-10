import { ApiError } from '../api/client';
import type { HoldStatus } from '../api/types';

export const FALLBACK_MESSAGE = 'সার্ভারে একটি সমস্যা হয়েছে। আবার চেষ্টা করুন।';

export const ERROR_MESSAGES: Record<string, string> = {
  SEATS_FULL: 'দুঃখিত, এই class-এ আর কোনো seat খালি নেই।',
  ALREADY_APPLIED: 'এই class-এ আপনার একটি চলমান আবেদন বা নিশ্চিত ভর্তি আগে থেকেই আছে।',
  HOLD_EXPIRED: 'আবেদনের মেয়াদ শেষ হয়ে গেছে। চাইলে আবার আবেদন করুন।',
  HOLD_CANCELLED: 'এই আবেদনটি বাতিল করা হয়েছে।',
  HOLD_ALREADY_CONFIRMED: 'ভর্তি নিশ্চিত হয়ে গেছে, এখন আর বাতিল করা যাবে না।',
  NOT_HOLD_OWNER: 'এই আবেদনটি আপনার নয়।',
  CLASS_NOT_FOUND: 'Class খুঁজে পাওয়া যায়নি।',
  HOLD_NOT_FOUND: 'আবেদনটি খুঁজে পাওয়া যায়নি।',
  STUDENT_ID_REQUIRED: 'সঠিক ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।',
  STUDENT_ID_INVALID: 'সঠিক ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।',
  VALIDATION_ERROR: 'দেওয়া তথ্য সঠিক নয়',
  NETWORK_ERROR: 'সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। একটু পরে আবার চেষ্টা করুন।',
  NOT_FOUND: 'যা খুঁজছেন তা পাওয়া যায়নি।',
  INTERNAL_ERROR: FALLBACK_MESSAGE,
};

/** Human-readable Bengali message for any error thrown anywhere in the app. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const base = ERROR_MESSAGES[error.code];
    if (!base) return FALLBACK_MESSAGE;
    if (error.code === 'VALIDATION_ERROR' && error.message) return `${base}: ${error.message}`;
    return base;
  }
  return FALLBACK_MESSAGE;
}

export function errorCode(error: unknown): string | null {
  return error instanceof ApiError ? error.code : null;
}

export const STATUS_LABELS: Record<HoldStatus, string> = {
  HELD: 'অপেক্ষমাণ (seat hold)',
  CONFIRMED: 'ভর্তি নিশ্চিত',
  EXPIRED: 'মেয়াদ শেষ',
  CANCELLED: 'বাতিল',
};
