const BENGALI_ZERO = 0x09e6; // '০'

/** Converts Bengali digits (০-৯) to ASCII and drops spaces/hyphens people type while copying. */
export function normalizeStudentId(input: string): string {
  return input
    .replace(/[০-৯]/g, (digit) => String(digit.charCodeAt(0) - BENGALI_ZERO))
    .replace(/[\s-]/g, '');
}

export function isValidStudentId(value: string): boolean {
  return /^[0-9]{17}$/.test(value);
}
