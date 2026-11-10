import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';
import { resetServerOffset } from '../lib/time';

afterEach(() => {
  cleanup();
  window.localStorage.clear();
  resetServerOffset();
});
