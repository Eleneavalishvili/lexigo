import { expect, test } from '@playwright/test';
import { registerLearner } from './test-account';
import { buySimulatedPremium } from './simulated-premium';

const inboxPort = process.env['LEXIGO_E2E_INBOX_PORT'];

test('simulated Premium unlocks guided audio and local recording', async ({
  page,
  request,
  baseURL,
}) => {
  test.skip(!inboxPort, 'Run with the disposable database and local inbox.');
  test.setTimeout(120_000);
  await page.addInitScript(() => {
    if (location.pathname !== '/speaking' || !navigator.mediaDevices?.getUserMedia) return;
    const getUserMedia = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
    let denied = false;
    navigator.mediaDevices.getUserMedia = (constraints) => {
      if (!denied) {
        denied = true;
        return Promise.reject(new DOMException('Test denial', 'NotAllowedError'));
      }
      return getUserMedia(constraints);
    };
  });
  await registerLearner(page, request, baseURL!, inboxPort!);
  await page.goto('/speaking');
  const start = page.getByRole('button', { name: 'Start guided practice' });
  await start.click();
  await expect(page.getByText('Guided practice is included in Premium.')).toBeVisible();

  await buySimulatedPremium(page);

  await page.goto('/speaking');
  await start.click();
  await expect(page.getByText('Word 1 of 3')).toBeVisible();
  const audio = page.waitForResponse(
    (response) => /\/pronunciation\/[a-f0-9]+\.wav$/.test(response.url()) && response.ok(),
  );
  await page.getByRole('button', { name: 'Listen slowly' }).click();
  await audio;
  await page.getByRole('button', { name: 'coffee', exact: true }).click();
  await expect(
    page.getByText(/means/).filter({ hasText: /Correct!|Try this meaning/ }),
  ).toBeVisible();

  await page.getByRole('button', { name: /Record your voice/ }).click();
  await expect(page.getByText(/Microphone access wasn’t allowed/)).toBeVisible();
  await expect(page.getByLabel('Your recorded voice')).toHaveCount(0);
  await page.getByRole('button', { name: /Record your voice/ }).click();
  await expect(page.getByText(/Recording · [1-9]/)).toBeVisible();
  await page.getByRole('button', { name: /Stop recording/ }).click();
  const recording = page.getByLabel('Your recorded voice');
  await expect(recording).toHaveAttribute('src', /^blob:/);
  await recording.evaluate((element: HTMLAudioElement) => element.play());
  await expect(recording).toHaveCount(0, { timeout: 10_000 });
  await page.getByRole('button', { name: 'Continue', exact: true }).click();
  await expect(page.getByText('Word 2 of 3')).toBeVisible();
});
