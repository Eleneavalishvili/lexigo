import { expect, test } from '@playwright/test';
import { registerLearner } from './test-account';

for (const course of [
  {
    id: '10000000-0000-0000-0000-000000000001',
    word: 'el café',
    example: 'El café está caliente.',
  },
  { id: '10000000-0000-0000-0000-000000000002', word: 'il caffè', example: 'Il caffè è caldo.' },
  {
    id: '10000000-0000-0000-0000-000000000003',
    word: 'der Kaffee',
    example: 'Der Kaffee ist heiß.',
  },
  { id: '10000000-0000-0000-0000-000000000004', word: 'кофе', example: 'Кофе горячий.' },
]) {
  test(`${course.word} can be heard, saved and reviewed with a durable schedule`, async ({
    page,
    request,
    baseURL,
  }) => {
    const inbox = process.env['LEXIGO_E2E_INBOX_PORT'];
    test.skip(!inbox, 'Requires disposable email and vocabulary services.');
    test.setTimeout(120_000);
    await registerLearner(page, request, baseURL!, inbox!);
    await page.goto('/vocabulary');
    await page.getByRole('button', { name: 'Discover words', exact: true }).click();
    await page.getByLabel('Language', { exact: true }).selectOption(course.id);
    await page.getByRole('button', { name: /Find words/ }).click();
    await expect(page.getByRole('article')).toHaveCount(20);
    const pages = page.getByRole('navigation', { name: 'Word pages' });
    await pages.getByRole('button', { name: /Next/ }).click();
    await expect(pages.getByText('Page 2', { exact: true })).toBeVisible();
    await expect(page.getByRole('article')).toHaveCount(2);
    await page.getByLabel('Word or English translation').fill('coffee');
    await page.getByRole('button', { name: /Find words/ }).click();
    const card = page.getByRole('article');
    await expect(card.getByRole('heading', { name: course.word, exact: true })).toBeVisible();
    await expect(card.getByText(course.example, { exact: true })).toBeVisible();
    const wordAudio = page.waitForResponse((response) =>
      /\/pronunciation\/[a-f0-9]{24}\.wav$/.test(new URL(response.url()).pathname),
    );
    await card.getByRole('button', { name: `Listen: ${course.word}`, exact: true }).click();
    expect((await wordAudio).ok()).toBeTruthy();
    await card.getByText('Hear the example', { exact: true }).click();
    const exampleAudio = page.waitForResponse((response) =>
      /\/pronunciation\/[a-f0-9]{24}\.wav$/.test(new URL(response.url()).pathname),
    );
    await card.getByRole('button', { name: `Listen: ${course.example}`, exact: true }).click();
    expect((await exampleAudio).ok()).toBeTruthy();
    await card.getByRole('button', { name: `Save ${course.word}`, exact: true }).click();
    await expect(
      card.getByRole('button', { name: `Remove ${course.word}`, exact: true }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Your saved words' }).click();
    await page.getByLabel('Review schedule').selectOption('due');
    await page.getByRole('button', { name: /Find words/ }).click();
    await page.getByRole('button', { name: 'Review word', exact: true }).click();
    await page.getByLabel('Type the English translation').fill('the coffee');
    await page.getByRole('button', { name: 'Check answer', exact: true }).click();
    await expect(page.getByText(/Correct!.*Next review/)).toBeVisible();
    await page.reload();
    await page.getByLabel('Review schedule').selectOption('scheduled');
    await page.getByRole('button', { name: /Find words/ }).click();
    await expect(
      page.getByRole('button', { name: `Remove ${course.word}`, exact: true }),
    ).toBeVisible();
    await expect(page.getByText(/Review from/)).toBeVisible();
  });
}
