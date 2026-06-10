import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'jsdom',
    include: ['ts-tests/**/*.test.ts'],
  },
});
