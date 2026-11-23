import { AxeBuilder } from '@axe-core/playwright';
import { expect, test, type Page, type TestInfo } from '@playwright/test';
import { registerLearner } from './test-account';
import { signInStaff } from './staff-account';

const inbox = process.env['LEXIGO_E2E_INBOX_PORT'];
const database = process.env['LEXIGO_E2E_DATABASE'];
const spanish = '10000000-0000-0000-0000-000000000001';

interface Finding {
  screen: string;
  rule: string;
  help: string;
  targets: string[];
  details?: string[];
}

async function inspect(
  page: Page,
  screen: string,
  info: TestInfo,
  findings: Finding[],
): Promise<void> {
  await expect(page.locator('main').first()).toBeVisible();
  await expect(
    page
      .getByRole('status')
      .filter({ hasText: /^(Loading|Opening|Checking|Fetching|Working|Preparing).*…$/ }),
  ).toHaveCount(0, { timeout: 20_000 });
  await page.evaluate(async () => {
    await document.fonts.ready;
  });
  const sizes =
    info.project.name === 'phone'
      ? [
          { width: 320, height: 568 },
          { width: 390, height: 844 },
        ]
      : [
          { width: 768, height: 1024 },
          { width: 1440, height: 900 },
        ];
  for (const size of sizes) {
    await page.setViewportSize(size);
    const label = `${screen} at ${size.width}px`;
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();
    findings.push(
      ...results.violations.map((violation) => ({
        screen: label,
        rule: violation.id,
        help: violation.help,
        targets: violation.nodes.map((node) => JSON.stringify(node.target)),
        details: violation.nodes.map((node) => node.failureSummary ?? ''),
      })),
    );
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth > window.innerWidth + 1,
    );
    if (overflow)
      findings.push({
        screen: label,
        rule: 'horizontal-overflow',
        help: 'The page exceeds the viewport width.',
        targets: ['html'],
      });
  }
}

async function finish(info: TestInfo, findings: Finding[]): Promise<void> {
  await info.attach('accessibility-findings', {
    body: JSON.stringify(findings, null, 2),
    contentType: 'application/json',
  });
  expect(findings, JSON.stringify(findings, null, 2)).toEqual([]);
}

test('public screens have named controls, readable contrast and responsive layouts', async ({
  page,
}, info) => {
  test.setTimeout(240_000);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  const findings: Finding[] = [];
  for (const path of [
    '/',
    '/courses',
    '/pricing',
    '/contact',
    '/login',
    '/register',
    '/forgot-password',
  ]) {
    await page.goto(path);
    await inspect(page, path, info, findings);
  }
  await page.goto('/register');
  const password = page.getByLabel('Make a password');
  await password.focus();
  await password.press('Tab');
  const reveal = page.getByRole('button', { name: 'Show password' });
  await expect(reveal).toBeFocused();
  await reveal.press('Space');
  await expect(password).toHaveAttribute('type', 'text');
  await page.getByRole('button', { name: 'Hide password' }).press('Space');
  await expect(password).toHaveAttribute('type', 'password');
  await finish(info, findings);
});

test('learner screens and a real first lesson remain accessible with long profile text', async ({
  page,
  request,
  baseURL,
}, info) => {
  test.skip(!inbox, 'Requires the disposable confirmation inbox.');
  test.setTimeout(300_000);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await registerLearner(page, request, baseURL!, inbox!, 'LongName'.repeat(8));
  const findings: Finding[] = [];
  await page.goto(`/learn/${spanish}`);
  await page.getByRole('link', { name: /Choose my goal and starting point/ }).click();
  await page.getByRole('button', { name: /20 XP \/ day/ }).click();
  await page.getByRole('button', { name: /Continue/ }).click();
  await page.getByRole('button', { name: /Start from scratch/ }).click();
  await page.getByRole('button', { name: /Let’s begin/ }).click();
  await expect(page).toHaveURL(/\/learn\/session\//, { timeout: 20_000 });
  await inspect(page, 'first lesson', info, findings);
  for (const path of [
    '/account',
    '/account/profile',
    '/classes',
    '/vocabulary',
    '/notifications',
    '/billing/cards',
    '/billing/history',
    '/leaderboard',
    '/achievements',
    '/challenges',
    '/activity',
  ]) {
    await page.goto(path);
    await inspect(page, path, info, findings);
  }
  await finish(info, findings);
});

test('staff management screens retain accessible forms and layouts', async ({
  page,
  request,
  baseURL,
}, info) => {
  test.skip(!inbox || !database, 'Requires disposable staff and email services.');
  test.setTimeout(300_000);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await signInStaff(page, request, baseURL!, inbox!, database!, [
    'Teacher',
    'ContentAuthor',
    'Reviewer',
    'Moderator',
    'Admin',
    'SuperAdmin',
  ]);
  const findings: Finding[] = [];
  for (const path of [
    '/manage/lessons',
    '/manage/imports',
    '/manage/skills',
    '/manage/rewards',
    '/manage/vocabulary',
    '/manage/media',
    '/manage/reports',
    '/admin/users',
    '/admin/analytics',
    '/admin/settings',
    '/admin/audit',
  ]) {
    await page.goto(path);
    await inspect(page, path, info, findings);
  }
  await finish(info, findings);
});
