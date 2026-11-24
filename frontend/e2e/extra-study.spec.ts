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

    await teacher.getByText('Schedule or change appointment', { exact: true }).click();
    await teacher
      .getByLabel(/Lesson time \(/)
      .fill(new Date(Date.now() + 86_400_000).toISOString().slice(0, 16));
    await teacher.getByLabel('Duration in minutes').fill('45');
    await teacher.getByRole('button', { name: 'Save appointment', exact: true }).click();
    await expect(teacher.getByText(/45 minutes/)).toBeVisible();

    await page.getByRole('button', { name: 'Refresh requests' }).click();
    await page.getByRole('link', { name: /Open study space/ }).click();
    await expect(page.getByText('Your first drinks')).toBeVisible();
    await expect(page.getByText(/45 minutes/)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Save appointment', exact: true })).toHaveCount(
      0,
    );
    await page.getByLabel('Your message').fill('Can we practise ordering coffee?');
    await page.getByRole('button', { name: 'Send message', exact: true }).click();
    await expect(page.getByRole('list', { name: 'Study conversation' })).toContainText(
      'Can we practise ordering coffee?',
    );
    await teacher.getByRole('button', { name: 'Refresh messages', exact: true }).click();
    await expect(teacher.getByRole('list', { name: 'Study conversation' })).toContainText(
      'Can we practise ordering coffee?',
    );
    await teacher.getByLabel('Your message').fill('Yes! We will practise a café conversation.');
    await teacher.getByRole('button', { name: 'Send message', exact: true }).click();
    await expect(teacher.getByRole('list', { name: 'Study conversation' })).toContainText(
      'Yes! We will practise a café conversation.',
    );
    await page.getByRole('button', { name: 'Refresh messages', exact: true }).click();
    await expect(page.getByRole('list', { name: 'Study conversation' })).toContainText(
      'Yes! We will practise a café conversation.',
    );
    await page.getByRole('button', { name: 'Start lesson' }).click();
    await expect(page).toHaveURL(/\/learn\/session\//);
  } finally {
    await teacher.close();
  }
});
