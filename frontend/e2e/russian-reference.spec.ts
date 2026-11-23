import { expect, test } from '@playwright/test';

test('all Russian letters are reachable by keyboard with name and example audio', async ({
  page,
  request,
}) => {
  await page.goto('/courses');
  await page.getByRole('button', { name: /Russian/ }).click();
  await page.getByRole('link', { name: /Meet all 33 Russian letters/ }).click();
  await expect(page).toHaveURL(/\/alphabet\/russian$/);
  const letters = page.getByRole('group', { name: 'Russian letters' });
  await expect(letters.getByRole('button')).toHaveCount(33);
  const first = letters.getByRole('button', { name: 'Letter А, а', exact: true });
  await first.focus();
  await first.press('End');
  await expect(letters.getByRole('button', { name: 'Letter Я, я', exact: true })).toBeFocused();
  await expect(page.locator('#selected-letter')).toContainText('Я');
  const manifestResponse = await request.get('/pronunciation/manifest.json');
  expect(manifestResponse.ok()).toBeTruthy();
  const manifest = (await manifestResponse.json()) as { clips: Record<string, { file: string }> };
  const dataResponse = await request.get('/teaching/russian-alphabet.json');
  expect(dataResponse.ok()).toBeTruthy();
  const data = (await dataResponse.json()) as {
    upper: string;
    audioText: string;
    example: string;
  }[];
  expect(data.map((letter) => letter.upper).join('')).toBe('АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ');
  for (const letter of data) {
    for (const text of [letter.audioText, letter.example]) {
      const key = 'ru|' + text.toLowerCase().replace(/[.!?]+$/, '');
      expect(manifest.clips[key]?.file).toMatch(/^[a-f0-9]{24}\.wav$/);
    }
  }
  const nameAudio = page.waitForResponse((response) =>
    /\/pronunciation\/[a-f0-9]{24}\.wav$/.test(new URL(response.url()).pathname),
  );
  await letters.getByRole('button', { name: 'Letter Ё, ё', exact: true }).click();
  expect((await nameAudio).ok()).toBeTruthy();
  await expect(page.getByText('Keep both dots:', { exact: false })).toBeVisible();
  const exampleAudio = page.waitForResponse((response) =>
    /\/pronunciation\/[a-f0-9]{24}\.wav$/.test(new URL(response.url()).pathname),
  );
  await page.getByRole('button', { name: 'Listen: ёлка', exact: true }).click();
  expect((await exampleAudio).ok()).toBeTruthy();
  await letters.getByRole('button', { name: 'Letter Ь, ь', exact: true }).click();
  await expect(
    page.getByText('The soft sign has no independent sound.', { exact: false }),
  ).toBeVisible();
  await expect(page.getByText('Reference study uses no hearts', { exact: false })).toBeVisible();
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1);
  expect(overflow).toBe(false);
});
