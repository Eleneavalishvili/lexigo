import { expect, test } from '@playwright/test';
import { buySimulatedPremium } from './simulated-premium';
import { signInStaff } from './staff-account';
import { registerLearner } from './test-account';

const database = process.env['LEXIGO_E2E_DATABASE'];
const inboxPort = process.env['LEXIGO_E2E_INBOX_PORT'];
const route = '/classes/extra-study';
const spanish = '10000000-0000-0000-0000-000000000001';

test('a Premium learner opens a lesson shared by their chosen teacher', async ({
  page,
  browser,
  request,
  baseURL,
}, testInfo) => {
  test.skip(!database || !inboxPort, 'Requires disposable staff and email services.');
  test.setTimeout(150_000);
  const teacher = await browser.newPage({ baseURL });
  const teacherName = `Spanish Tutor ${testInfo.project.name}`;
  try {
    await signInStaff(teacher, request, baseURL!, inboxPort!, database!, ['Teacher'], teacherName);
    await teacher.goto(route);
    await teacher.getByRole('button', { name: 'Spanish · Hidden', exact: true }).click();
    await expect(
      teacher.getByRole('button', { name: 'Spanish · Listed', exact: true }),
    ).toBeVisible();

    await registerLearner(page, request, baseURL!, inboxPort!);
    await page.goto(route);
    await page.getByLabel('Course', { exact: true }).selectOption(spanish);
    await expect(
      page.getByText('Teacher listings are available to learners with active Premium.'),
    ).toBeVisible();
    await buySimulatedPremium(page);
    await page.goto(`/learn/${spanish}`);
    await page.getByRole('link', { name: /Choose my goal and starting point/ }).click();
    await page.getByRole('button', { name: /20 XP \/ day/ }).click();
    await page.getByRole('button', { name: /Continue/ }).click();
    await page.getByRole('button', { name: /Start from scratch/ }).click();
    await page.getByRole('button', { name: /Let’s begin/ }).click();
    await expect(page).toHaveURL(/\/learn\/session\//);

    await page.goto(route);
    await page.getByLabel('Course', { exact: true }).selectOption(spanish);
    await page
      .getByRole('listitem')
      .filter({ hasText: teacherName })
      .getByRole('button', { name: 'Request extra study' })
      .click();
    await expect(page.getByText(/Spanish · Pending/)).toBeVisible();
    await teacher.getByRole('button', { name: 'Refresh requests' }).click();
    await teacher.getByRole('button', { name: 'Accept', exact: true }).click();
    await teacher.getByRole('link', { name: /Open study space/ }).click();
    await teacher.getByLabel('Unit', { exact: true }).selectOption({ label: 'Your first words' });
    await teacher
      .getByLabel('Lesson', { exact: true })
      .selectOption({ label: 'Your first drinks' });
    await teacher.getByRole('button', { name: 'Share lesson' }).click();
    await expect(
      teacher.getByRole('region', { name: 'Shared lessons' }).getByText('Your first drinks'),
    ).toBeVisible();

    await page.getByRole('button', { name: 'Refresh requests' }).click();
    await page.getByRole('link', { name: /Open study space/ }).click();
    await expect(page.getByText('Your first drinks')).toBeVisible();
    await page.getByRole('button', { name: 'Start lesson' }).click();
    await expect(page).toHaveURL(/\/learn\/session\//);
  } finally {
    await teacher.close();
  }
});
