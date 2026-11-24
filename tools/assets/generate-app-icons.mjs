import { readFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(new URL('../../frontend/package.json', import.meta.url));
const { chromium } = require('@playwright/test');
const brand = new URL('../../frontend/public/brand/', import.meta.url);
const mark = await readFile(new URL('lexigo-mark.svg', brand), 'utf8');
const browser = await chromium.launch({
  channel: process.argv[2] || undefined,
});

try {
  const page = await browser.newPage({ deviceScaleFactor: 1 });
  for (const [size, name] of [
    [180, 'apple-touch-icon.png'],
    [192, 'app-icon-192.png'],
    [512, 'app-icon-512.png'],
  ]) {
    await page.setViewportSize({ width: size, height: size });
    await page.setContent(`<!doctype html><html><head><style>
      body { margin: 0; height: 100vh; display: grid; place-items: center; background: #f7f1e3; }
      svg { width: 64vw; height: 64vh; }
    </style></head><body>${mark}</body></html>`);
    await page.screenshot({ path: fileURLToPath(new URL(name, brand)) });
    console.log(`Exported ${name}: ${size} × ${size}, using the unchanged LexiGo mark.`);
  }
} finally {
  await browser.close();
}
