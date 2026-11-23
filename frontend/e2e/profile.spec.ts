import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { registerLearner } from './test-account';

test('a learner saves profile settings, exports their data and confirms account deletion', async ({
  page,
  request,
  baseURL,
}) => {
  const inbox = process.env['LEXIGO_E2E_INBOX_PORT'];
  test.skip(
    !inbox || process.env['LEXIGO_E2E_PRIVATE_AUDIO'] !== 'true',
    'Requires disposable email and object storage.',
  );
  test.setTimeout(90_000);
  const account = await registerLearner(page, request, baseURL!, inbox!);
  await page.goto('/account/profile');
  await page.getByLabel('Display name').fill('Profile Test Learner');
  await page.getByRole('button', { name: /Save name/ }).click();
  await expect(page.getByText('Your display name is saved.', { exact: false })).toBeVisible();

  const active = page.getByLabel('Your active language');
  await active.selectOption({ label: 'Spanish' });
  await page.getByLabel('Time zone').fill('Asia/Tbilisi');
  await page.getByLabel('Daily XP goal').fill('45');
  const monday = page.getByRole('button', { name: 'Monday', exact: true });
  const pressedBefore = await monday.getAttribute('aria-pressed');
  await monday.click();
  const pressedAfter = pressedBefore === 'true' ? 'false' : 'true';
  await expect(monday).toHaveAttribute('aria-pressed', pressedAfter);
  await page.getByRole('button', { name: 'Save my little preferences' }).click();
  await expect(page.getByText('Your study settings are saved.', { exact: false })).toBeVisible();

  const png = await page.evaluate(() => {
    const canvas = document.createElement('canvas');
    canvas.width = 24;
    canvas.height = 16;
    const context = canvas.getContext('2d')!;
    context.fillStyle = '#25798b';
    context.fillRect(0, 0, 24, 16);
    return canvas.toDataURL('image/png').split(',')[1]!;
  });
  const photo = page.getByRole('img', { name: 'Profile photo' });
  await page.getByLabel('Choose photo').setInputFiles({
    name: 'test-photo.png',
    mimeType: 'image/png',
    buffer: Buffer.from(png, 'base64'),
  });
  await expect(photo).toHaveAttribute('src', /^blob:/);
  await page.getByRole('button', { name: 'Save photo' }).click();
  await expect(page.getByText('Profile photo saved.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Remove photo' })).toBeEnabled();
  await page.reload();
  await expect(page.getByLabel('Display name')).toHaveValue('Profile Test Learner');
  await expect(active.locator('option:checked')).toHaveText('Spanish');
  await expect(page.getByLabel('Time zone')).toHaveValue('Asia/Tbilisi');
  await expect(page.getByLabel('Daily XP goal')).toHaveValue('45');
  await expect(monday).toHaveAttribute('aria-pressed', pressedAfter);
  await expect
    .poll(() => photo.evaluate((image: HTMLImageElement) => image.naturalWidth))
    .toBe(128);

  const [download] = await Promise.all([
    page.waitForEvent('download'),
    page.getByRole('button', { name: 'Download my learning data', exact: true }).click(),
  ]);
  expect(download.suggestedFilename()).toBe('lexigo-learning-data.json');
  const downloadPath = await download.path();
  expect(downloadPath).not.toBeNull();
  const exportedText = await readFile(downloadPath!, 'utf8');
  const exported = JSON.parse(exportedText);
  expect(exported.schemaVersion).toBe(1);
  expect(exported.profile.email).toBe(account.email);
  expect(exported.profile.displayName).toBe('Profile Test Learner');
  expect(exported.preferences.timeZone).toBe('Asia/Tbilisi');
  expect(exported.preferences.dailyGoalXp).toBe(45);
  expect(exported.preferences.leaderboardPhotoShared).toBe(false);
  for (const section of [
    'lessonProgress',
    'exerciseAttempts',
    'xpHistory',
    'contentReports',
    'uploadedMedia',
  ]) {
    expect(Array.isArray(exported[section])).toBe(true);
  }
  expect(exportedText).not.toContain(account.password);
  for (const privateField of [
    'passwordHash',
    'securityStamp',
    'privateEvaluationSnapshot',
    'refreshToken',
  ]) {
    expect(exportedText).not.toContain(privateField);
  }
  await page.getByRole('button', { name: 'Remove photo' }).click();
  await expect(page.getByText('Profile photo removed.')).toBeVisible();
  const emptyPhoto = page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname === '/api/v1/me/avatar' &&
      response.request().method() === 'GET',
  );
  await page.reload();
  const photoResponse = await emptyPhoto;
  expect(photoResponse.ok()).toBe(true);
  await expect(photo).toHaveCount(0);

  await page.goto('/account/delete');
  await expect(
    page.getByText('You have no unfinished teaching, draft or recording responsibilities.'),
  ).toBeVisible();
  const deleteAccount = page.getByRole('button', { name: 'Permanently delete my account' });
  await expect(deleteAccount).toBeDisabled();
  await page.getByLabel('Current password', { exact: true }).fill(account.password);
  await page.getByLabel('Type DELETE to confirm permanent deletion').fill('DELETE');
  const [deletion] = await Promise.all([
    page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === '/api/v1/me' &&
        response.request().method() === 'DELETE',
    ),
    deleteAccount.click(),
  ]);
  expect(deletion.status()).toBe(204);
  await expect(page.getByRole('heading', { name: 'Your account has been deleted.' })).toBeVisible();
  await page.goto('/account/profile');
  await expect(page).toHaveURL(/\/login(?:\?|$)/);
});
