import { randomUUID } from 'node:crypto';
import { expect, type APIRequestContext, type Page } from '@playwright/test';

interface InboxMessage {
  ID: string;
  To: { Address: string }[];
}

export async function registerLearner(
  page: Page,
  request: APIRequestContext,
  baseURL: string,
  inboxPort: string,
  displayName = 'Browser Learner',
): Promise<{ email: string; password: string }> {
  const inbox = `http://127.0.0.1:${inboxPort}/api/v1`;
  const email = `learner-${randomUUID()}@example.test`;
  const password = `Test!${randomUUID()}`;

  await page.goto('/register');
  await page.getByLabel('What should we call you?').fill(displayName);
  await page.getByLabel('Your email').fill(email);
  await page.getByLabel('Make a password').fill(password);
  await page.getByRole('button', { name: /Create my free account/ }).click();
  await expect(page.getByText('a confirmation email is on its way', { exact: false })).toBeVisible({
    timeout: 20_000,
  });

  let messageId: string | undefined;
  await expect
    .poll(
      async () => {
        const response = await request.get(`${inbox}/messages`);
        expect(response.ok()).toBeTruthy();
        const { messages }: { messages: InboxMessage[] } = await response.json();
        messageId = messages.find((message) => message.To.some((to) => to.Address === email))?.ID;
        return Boolean(messageId);
      },
      { timeout: 35_000 },
    )
    .toBe(true);

  const response = await request.get(`${inbox}/message/${messageId}`);
  expect(response.ok()).toBeTruthy();
  const message: { HTML: string } = await response.json();
  const link = message.HTML.match(/href="([^"]+\/verify-email\?[^"]+)"/)?.[1];
  expect(Boolean(link)).toBe(true);
  const confirmation = new URL(link!.replaceAll('&amp;', '&'));
  expect(confirmation.origin).toBe(new URL(baseURL!).origin);
  await page.goto(confirmation.href);
  await page.getByRole('button', { name: /Yes, confirm my email/ }).click();
  await expect(page.getByText('Your email address is verified.', { exact: false })).toBeVisible();

  await page.getByRole('link', { name: /Let’s sign in/ }).click();
  await enterCredentials(page, email, password);
  await expect(page).not.toHaveURL(/\/login(?:\?|$)/);
  return { email, password };
}

export async function enterCredentials(page: Page, email: string, password: string): Promise<void> {
  await page.getByLabel('Your email').fill(email);
  await page.getByLabel('Your password').fill(password);
  await page.getByRole('button', { name: /Let me in/ }).click();
}
