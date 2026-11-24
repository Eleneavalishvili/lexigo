import { defineConfig, devices } from '@playwright/test';
import base from './playwright.config';

export default defineConfig({
  ...base,
  use: { ...base.use, channel: undefined, launchOptions: {} },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Safari'], browserName: 'webkit' } },
    { name: 'phone', use: { ...devices['iPhone 13'], browserName: 'webkit' } },
  ],
});
