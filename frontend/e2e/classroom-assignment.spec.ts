import { expect, test } from '@playwright/test';
import { signInStaff } from './staff-account';
import { registerLearner } from './test-account';

test('a teacher invites a learner and assigns a published lesson', async ({
  page,
  browser,
  request,
  baseURL,
}, testInfo) => {
  const database = process.env['LEXIGO_E2E_DATABASE'];
  const inbox = process.env['LEXIGO_E2E_INBOX_PORT'];
  test.skip(!database || !inbox, 'Requires disposable staff and email services.');
  test.setTimeout(180_000);
  const teacher = await browser.newPage({ ...testInfo.project.use, baseURL });
  const replacement = await browser.newPage({ ...testInfo.project.use, baseURL });
  const title = `Spanish practice ${testInfo.project.name}`;
  try {
    await signInStaff(teacher, request, baseURL!, inbox!, database!, ['Teacher']);
    await teacher.goto('/classes');
    await teacher.getByLabel('Course', { exact: true }).selectOption({ label: 'Spanish' });
    await teacher.getByLabel('Class name').fill(title);
    await teacher.getByRole('button', { name: /Create class/ }).click();
    await expect(teacher.getByRole('heading', { name: title })).toBeVisible();
    const classUrl = teacher.url();
    await teacher.getByRole('button', { name: 'Generate new code' }).click();
    const code = await teacher.getByLabel('Your new invitation code').inputValue();

    await registerLearner(page, request, baseURL!, inbox!);
    await page.goto('/classes');
    await page.getByLabel('Invitation code').fill(code);
    await page.getByRole('button', { name: /Join class/ }).click();
    await expect(page.getByRole('heading', { name: title })).toBeVisible();

    await teacher.getByRole('button', { name: 'View assignments' }).click();
    await teacher.getByRole('button', { name: 'New assignment' }).click();
    await teacher.getByLabel('Title', { exact: true }).fill('First drinks review');
    const deadline = new Date(Date.now() + 3 * 86_400_000).toISOString().slice(0, 16);
    await teacher.getByLabel('Deadline (your local time)').fill(deadline);
    await teacher.getByLabel('Minimum score (%)').fill('85');
    await teacher
      .getByLabel('Find published lessons by unit')
      .selectOption({ label: 'Your first words' });
    await teacher.getByRole('checkbox', { name: 'Your first drinks' }).check();
    const [created] = await Promise.all([
      teacher.waitForResponse(
        (response) =>
          response.request().method() === 'POST' &&
          /\/api\/v1\/classes\/[^/]+\/assignments$/.test(new URL(response.url()).pathname),
      ),
      teacher.getByRole('button', { name: 'Create assignment', exact: true }).click(),
    ]);
    expect(created.status()).toBe(201);
    expect(await created.headerValue('location')).toMatch(/\/api\/v1\/assignments\/[a-f0-9-]{36}$/);
    await expect(teacher.getByRole('button', { name: 'First drinks review' })).toBeVisible();

    await expect(page.getByRole('button', { name: 'Hide assignments' })).toBeVisible();
    await page.getByRole('button', { name: /Refresh assignments/ }).click();
    await page.getByRole('button', { name: 'First drinks review' }).click();
    await expect(page.getByText(/Minimum score 85%/).first()).toBeVisible();
    await expect(page.getByText('Your first drinks', { exact: false }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Edit assignment' })).toHaveCount(0);

    const [replacementRegistration] = await Promise.all([
      replacement.waitForRequest(
        (sent) =>
          new URL(sent.url()).pathname === '/api/v1/auth/register' && sent.method() === 'POST',
      ),
      signInStaff(replacement, request, baseURL!, inbox!, database!, ['Teacher']),
    ]);
    const replacementEmail: string = replacementRegistration.postDataJSON().email;
    await teacher.getByLabel('Receiving teacher’s email').fill(replacementEmail);
    await teacher.getByRole('button', { name: 'Create handover link' }).click();
    const handoverLink = await teacher.getByLabel('Class handover link').inputValue();
    await replacement.goto(handoverLink);
    await expect(replacement.getByRole('heading', { name: title })).toBeVisible();
    await replacement.getByRole('button', { name: 'Accept responsibility for this class' }).click();
    await expect(replacement).toHaveURL(classUrl);
    await expect(replacement.getByRole('heading', { name: 'Members', exact: true })).toBeVisible();
    await replacement.getByRole('button', { name: 'View assignments' }).click();
    await expect(replacement.getByRole('button', { name: 'First drinks review' })).toBeVisible();
    await teacher.reload();
    await expect(teacher.getByRole('alert')).toContainText('This class is unavailable');
    await page.reload();
    await expect(page.getByRole('heading', { name: title })).toBeVisible();
  } finally {
    await teacher.close();
    await replacement.close();
  }
});
