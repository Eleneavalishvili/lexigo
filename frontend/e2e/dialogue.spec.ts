import { expect, test } from '@playwright/test';
import { registerLearner } from './test-account';
import { buySimulatedPremium } from './simulated-premium';

const inboxPort = process.env['LEXIGO_E2E_INBOX_PORT'];
const spanish = '10000000-0000-0000-0000-000000000001';

test('all four courses offer playable Free conversations', async ({ page, request, baseURL }) => {
  test.skip(!inboxPort, 'Requires the disposable email service.');
  test.setTimeout(160_000);
  await registerLearner(page, request, baseURL!, inboxPort!);
  const corrections = ['¡Date prisa!', 'Sbrigati!', 'Beeil dich!', 'Поторопись!'];
  for (const [index, correction] of corrections.entries()) {
    const number = index + 1;
    const course = `/learn/10000000-0000-0000-0000-00000000000${number}`;
    await page.goto(course);
    await page.getByRole('link', { name: /Choose my goal and starting point/ }).click();
    await page.getByRole('button', { name: /20 XP \/ day/ }).click();
    await page.getByRole('button', { name: /Continue/ }).click();
    await page.getByRole('button', { name: /Start from scratch/ }).click();
    await page.getByRole('button', { name: /Let’s begin/ }).click();
    await expect(page).toHaveURL(/\/learn\/session\//, { timeout: 20_000 });
    await page.goto(`${course}/dialogues`);
    for (const title of [
      'At the café',
      'At the hotel',
      'Buying a ticket',
      'Asking for directions',
    ]) {
      await page.getByRole('button', { name: `Open conversation: ${title}`, exact: true }).click();
      const choices = page.locator('.choices button');
      await expect(choices).toHaveCount(3);
      await choices.first().click();
      const advance = page.getByRole('button', { name: /^(Continue|See conversation summary)$/ });
      await expect(advance).toBeVisible();
      const hasFollowUp = (await advance.innerText()).trim() === 'Continue';
      await advance.click();
      if (hasFollowUp) {
        await choices.first().click();
        await page.getByRole('button', { name: 'See conversation summary' }).click();
      }
      await expect(page.getByRole('heading', { name: 'The path you took' })).toBeVisible();
      await expect(page.getByRole('region', { name: 'Your conversation reward' })).toContainText(
        hasFollowUp ? '+10 XP' : '0 XP for this attempt',
      );
      await expect(page.getByRole('heading', { name: 'Phrases to practise' })).toBeVisible();
      await page.getByRole('link', { name: /All conversations/ }).click();
      await expect(
        page.getByRole('button', { name: `Replay conversation: ${title}`, exact: true }),
      ).toBeVisible();
      await page
        .getByRole('button', { name: `Replay conversation: ${title}`, exact: true })
        .click();
      await page.getByRole('button', { name: correction, exact: true }).click();
      await expect(page.getByRole('heading', { name: 'Try this next time' })).toBeVisible();
      await expect(page.locator('#dialogue-feedback')).toContainText('specific, polite request');
      await page.getByRole('button', { name: 'See conversation summary' }).click();
      const phrases = page.getByRole('region', { name: 'Phrases to practise' });
      await expect(page.getByRole('region', { name: 'Your conversation reward' })).toContainText(
        '0 XP for this attempt',
      );
      await expect(phrases.locator('li')).toHaveCount(1);
      await expect(phrases).not.toContainText(correction);
      await expect(page.getByRole('region', { name: 'The path you took' })).toContainText(
        correction,
      );
      await page.getByRole('link', { name: /All conversations/ }).click();
    }
  }
});

test('a learner finishes a Free conversation and unlocks another with simulated Premium', async ({
  page,
  request,
  baseURL,
}) => {
  test.skip(!inboxPort, 'Run with the disposable database and local inbox.');
  test.setTimeout(90_000);
  await registerLearner(page, request, baseURL!, inboxPort!);
  await page.goto(`/learn/${spanish}`);
  await page.getByRole('link', { name: /Choose my goal and starting point/ }).click();
  await page.getByRole('button', { name: /20 XP \/ day/ }).click();
  await page.getByRole('button', { name: /Continue/ }).click();
  await page.getByRole('button', { name: /Start from scratch/ }).click();
  await page.getByRole('button', { name: /Let’s begin/ }).click();
  await expect(page).toHaveURL(/\/learn\/session\//, { timeout: 20_000 });

  await page.goto(`/learn/${spanish}/dialogues`);
  const premium = page.getByRole('listitem').filter({ hasText: 'At lost and found' });
  await expect(premium.getByRole('link', { name: /See Premium plans/ })).toHaveAttribute(
    'href',
    '/pricing',
  );
  const [created] = await Promise.all([
    page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        /\/api\/v1\/dialogues\/[^/]+\/sessions$/.test(new URL(response.url()).pathname),
    ),
    page.getByRole('button', { name: 'Open conversation: At the café' }).click(),
  ]);
  expect(created.status()).toBe(201);
  expect(await created.headerValue('location')).toMatch(
    /\/api\/v1\/me\/dialogue-sessions\/[a-f0-9-]{36}$/,
  );
  await page.getByRole('button', { name: 'Un café, por favor.' }).click();
  await expect(page.getByRole('heading', { name: 'Good response' })).toBeVisible();
  await page.getByRole('button', { name: 'Continue', exact: true }).click();
  await page.getByRole('button', { name: 'Con leche, gracias.' }).click();
  await page.getByRole('button', { name: 'See conversation summary' }).click();
  await expect(page.getByRole('heading', { name: 'The path you took' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Phrases to practise' })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('heading', { name: 'The path you took' })).toBeVisible();

  await buySimulatedPremium(page);
  await page.goto(`/learn/${spanish}/dialogues`);
  await premium.getByRole('button', { name: 'Open conversation: At lost and found' }).click();
  await page.getByRole('button', { name: 'He perdido una mochila.', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Good response' })).toBeVisible();
  await page.getByRole('button', { name: 'Continue', exact: true }).click();
  await page.getByRole('button', { name: 'Es azul.', exact: true }).click();
  await page.getByRole('button', { name: 'See conversation summary' }).click();
  const path = page.getByRole('region', { name: 'The path you took' });
  await expect(path).toContainText('He perdido una mochila.');
  await expect(path).toContainText('Es azul.');
  await page.reload();
  await expect(path).toContainText('Es azul.');
});
