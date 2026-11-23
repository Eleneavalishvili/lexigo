import { expect, test, type Page } from '@playwright/test';
import { signInStaff } from './staff-account';
import { registerLearner } from './test-account';

async function completePublishedExercises(
  page: Page,
  title: string,
  withAudio: boolean,
): Promise<void> {
  await page.goto('/learn/10000000-0000-0000-0000-000000000001');
  await page.getByRole('link', { name: /Choose my goal and starting point/ }).click();
  await page.getByRole('button', { name: /20 XP \/ day/ }).click();
  await page.getByRole('button', { name: /Continue/ }).click();
  await page.getByRole('button', { name: /Start from scratch/ }).click();
  await page.getByRole('button', { name: /Let’s begin/ }).click();
  await expect(page).toHaveURL(/\/learn\/session\//);
  await page.goto('/learn/10000000-0000-0000-0000-000000000001');
  const lesson = page.locator('details').filter({
    has: page.locator('summary').filter({ hasText: title }),
  });
  await lesson.locator('summary').click();
  await lesson.getByRole('button', { name: /Start lesson/ }).click();

  const check = async () => {
    await page.getByRole('button', { name: 'Check', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Nice!' })).toBeVisible();
    await page.getByRole('button', { name: /Continue/ }).click();
  };

  await page.getByLabel('Your translation').fill('Hola');
  await check();
  for (const tile of ['Yo', 'bebo', 'agua']) {
    await page.getByRole('button', { name: new RegExp(`^Add ${tile}, tile`) }).click();
  }
  await check();
  await page.getByRole('combobox', { name: 'Match for Hola' }).selectOption({ label: 'Hello' });
  await page.getByRole('combobox', { name: 'Match for Agua' }).selectOption({ label: 'Water' });
  await check();
  await page.getByLabel('Missing word or phrase').fill('agua');
  await check();
  await page.getByLabel('Your translation').fill('Water');
  await check();
  await expect(page.getByText('Ana bebe agua. Luis bebe leche.', { exact: true })).toBeVisible();
  await page.getByRole('radio', { name: 'Ana', exact: true }).check();
  await check();
  if (withAudio) {
    await page.getByRole('button', { name: /Listen to recording/ }).click();
    const audio = page.getByRole('region', { name: 'Lesson recording' }).locator('audio');
    await audio.evaluate((element) => (element as HTMLAudioElement).play());
    await expect
      .poll(() => audio.evaluate((element) => (element as HTMLAudioElement).currentTime))
      .toBeGreaterThan(0);
    await page.getByLabel('What did you hear?').fill('Tone');
    await check();
  }
  await page.getByRole('button', { name: /Finish & see results/ }).click();
  const result = withAudio ? '7 / 7' : '6 / 6';
  await expect(page.locator('app-lesson-result-stats').getByText(result)).toBeVisible();
}

function previewRecording(): Buffer {
  const sampleRate = 8000;
  const headerSize = 44;
  const wav = Buffer.alloc(headerSize + sampleRate * 2);
  wav.write('RIFF', 0);
  wav.writeUInt32LE(wav.length - 8, 4);
  wav.write('WAVEfmt ', 8);
  wav.writeUInt32LE(16, 16);
  wav.writeUInt16LE(1, 20);
  wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(sampleRate, 24);
  wav.writeUInt32LE(sampleRate * 2, 28);
  wav.writeUInt16LE(2, 32);
  wav.writeUInt16LE(16, 34);
  wav.write('data', 36);
  wav.writeUInt32LE(sampleRate * 2, 40);
  for (let sample = 0; sample < sampleRate; sample++) {
    wav.writeInt16LE(
      Math.round(1200 * Math.sin((2 * Math.PI * 440 * sample) / sampleRate)),
      headerSize + sample * 2,
    );
  }
  return wav;
}

const database = process.env['LEXIGO_E2E_DATABASE'];
const inboxPort = process.env['LEXIGO_E2E_INBOX_PORT'];

function waitForLessonList(page: Page) {
  return page.waitForResponse(
    (response) =>
      response.request().method() === 'GET' &&
      new URL(response.url()).pathname.endsWith('/manage/lessons'),
    { timeout: 15_000 },
  );
}

async function openWorkshop(page: Page): Promise<void> {
  await page.goto('/manage/lessons');
  await Promise.all([
    waitForLessonList(page),
    page.getByRole('button', { name: 'Spanish', exact: true }).click(),
  ]);
  await expect(page.getByText('Opening the workshop…')).toBeHidden();
}

async function chooseLesson(page: Page, title: string): Promise<void> {
  const lesson = page.getByRole('button').filter({
    has: page.getByText(title, { exact: true }),
  });
  const next = page.getByRole('button', { name: /Next →/ });
  for (let index = 0; index < 10; index++) {
    await expect(page.getByText(/VISIBLE LESSONS/)).toBeVisible();
    if (await lesson.isVisible()) break;
    if (!(await next.isEnabled())) break;
    await Promise.all([waitForLessonList(page), next.click()]);
    await expect(page.getByText('Opening the workshop…')).toBeHidden();
  }
  await lesson.click();
}

test('an independent reviewer approves a draft before an administrator publishes it', async ({
  page,
  browser,
  request,
  baseURL,
}, testInfo) => {
  test.skip(!database || !inboxPort, 'Requires disposable staff and email services.');
  test.setTimeout(330_000);
  const title = `Browser review lesson ${testInfo.project.name}`;
  const staffPageOptions = { ...testInfo.project.use, baseURL };
  const reviewer = await browser.newPage(staffPageOptions);
  const publisher = await browser.newPage(staffPageOptions);
  try {
    await signInStaff(page, request, baseURL!, inboxPort!, database!, [
      'ContentAuthor',
      'Reviewer',
    ]);
    await openWorkshop(page);
    await expect(page.getByRole('button', { name: 'German', exact: true })).toHaveCount(0);
    await page.getByText('Create a lesson draft', { exact: true }).click();
    if (testInfo.project.name === 'phone') {
      await page.getByLabel('Unit', { exact: true }).selectOption({ label: 'First conversations' });
    }
    await page.getByLabel('Lesson title').fill(title);
    await page.getByLabel('Learning objective').fill('Recognize a simple greeting.');
    await page.getByLabel('English grammar explanation').fill('Hola is a common greeting.');
    const creation = page.waitForResponse(
      (response) =>
        response.url().endsWith('/manage/lessons') && response.request().method() === 'POST',
    );
    await page.getByRole('button', { name: /Create draft/ }).click();
    const created = await creation;
    expect(created.status()).toBe(201);
    const { id }: { id: string } = await created.json();
    expect(id).toMatch(/^[a-f0-9-]{36}$/i);
    expect(await created.headerValue('location')).toMatch(
      new RegExp(`/api/v1/manage/lessons/${id}$`),
    );
    const publicLesson = `/api/v1/lessons/${id}`;
    expect((await request.get(publicLesson)).status()).toBe(404);
    expect(
      (
        await request.post(`/api/v1/manage/lessons/${id}/preview/check`, {
          data: { versionId: id, exerciseId: id, answer: 'Hola' },
        })
      ).status(),
    ).toBe(401);

    await page.getByText('Add a draft exercise', { exact: true }).click();
    await page.getByLabel('Exercise type').selectOption('Translation');
    await page.getByLabel('English instruction or question').fill('Write hello in Spanish.');
    await page.getByLabel('Feedback explanation in English').fill('Use hola as a greeting.');
    await page.getByLabel('Accepted answers, one per line').fill('Hola');
    await page.getByRole('button', { name: /Add to draft/ }).click();
    await expect(page.getByRole('heading', { name: 'Exercises · 1' })).toBeVisible();
    await page.getByRole('button', { name: 'Preview as learner' }).click();
    const preview = page.getByRole('region', { name: 'Learner preview' });
    await expect(preview.getByRole('heading', { name: 'Write hello in Spanish.' })).toBeVisible();
    await expect(preview.getByText('Staff answer key', { exact: false })).toHaveCount(0);
    await expect(preview.getByRole('button', { name: 'Next exercise' })).toBeDisabled();
    await preview.getByLabel('Preview answer').fill('adiós');
    await preview.getByRole('button', { name: 'Check preview answer' }).click();
    await expect(preview.getByText('Incorrect.', { exact: true })).toBeVisible();
    await preview.getByLabel('Preview answer').fill('Hola');
    await preview.getByRole('button', { name: 'Check preview answer' }).click();
    await expect(preview.getByText('Correct.', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Close learner preview' }).click();
    await expect(preview).toHaveCount(0);

    if (!(await page.getByLabel('Exercise type').isVisible())) {
      await page.getByText('Add a draft exercise', { exact: true }).click();
    }
    await page.getByLabel('Exercise type').selectOption('SentenceBuilder');
    await page
      .getByLabel('English instruction or question')
      .fill('Build “I drink water” in Spanish.');
    await page.getByLabel('Feedback explanation in English').fill('Yo bebo agua.');
    await page.getByLabel('Word tiles, one per line', { exact: false }).fill('agua\nYo\nbebo');
    await page.getByLabel('Accepted tile orders, one per line').fill('2 3 1');
    await page.getByRole('button', { name: /Add to draft/ }).click();
    await expect(page.getByRole('heading', { name: 'Exercises · 2' })).toBeVisible();

    if (!(await page.getByLabel('Exercise type').isVisible())) {
      await page.getByText('Add a draft exercise', { exact: true }).click();
    }
    await page.getByLabel('Exercise type').selectOption('Matching');
    await page.getByLabel('English instruction or question').fill('Match the Spanish words.');
    await page
      .getByLabel('Feedback explanation in English')
      .fill('Hola means hello and agua means water.');
    await page.getByLabel('Left items, one per line').fill('Hola\nAgua');
    await page.getByLabel('Their matches, in the same order').fill('Hello\nWater');
    await page.getByRole('button', { name: /Add to draft/ }).click();
    await expect(page.getByRole('heading', { name: 'Exercises · 3' })).toBeVisible();

    await expect(page.getByRole('button', { name: 'Move exercise 1 up' })).toBeDisabled();
    await expect(page.getByRole('button', { name: 'Move exercise 3 down' })).toBeDisabled();
    await page.getByRole('button', { name: 'Move exercise 2 up' }).click();
    const exerciseList = page.locator('.exercise-list > li');
    await expect(exerciseList.first()).toContainText('Build “I drink water” in Spanish.');
    await page.getByRole('button', { name: 'Move exercise 1 down' }).click();
    await expect(exerciseList.first()).toContainText('Write hello in Spanish.');

    await page.getByRole('button', { name: 'Preview as learner' }).click();
    const advance = preview.getByRole('button', { name: 'Next exercise' });
    const check = preview.getByRole('button', { name: 'Check preview answer' });
    await advance.click();
    await expect(check).toBeDisabled();
    for (const tile of ['agua', 'Yo', 'bebo']) {
      await preview.getByRole('button', { name: new RegExp(`^Add ${tile}, tile`) }).click();
    }
    await check.click();
    await expect(preview.getByText('Incorrect.', { exact: true })).toBeVisible();
    await preview.getByRole('button', { name: 'Move word at position 1 later' }).click();
    await expect(preview.getByText('Your sentence: Yo agua bebo', { exact: true })).toBeVisible();
    await preview.getByRole('button', { name: 'Move word at position 2 later' }).click();
    await expect(preview.getByText('Your sentence: Yo bebo agua', { exact: true })).toBeVisible();
    await check.click();
    await expect(preview.getByText('Correct.', { exact: true })).toBeVisible();
    await advance.click();
    await preview
      .getByRole('combobox', { name: 'Match for Hola' })
      .selectOption({ label: 'Water' });
    await expect(check).toBeDisabled();
    await preview
      .getByRole('combobox', { name: 'Match for Agua' })
      .selectOption({ label: 'Hello' });
    await check.click();
    await expect(preview.getByText('Incorrect.', { exact: true })).toBeVisible();
    await preview.getByRole('button', { name: /Clear connections/ }).click();
    await preview
      .getByRole('combobox', { name: 'Match for Hola' })
      .selectOption({ label: 'Hello' });
    await preview
      .getByRole('combobox', { name: 'Match for Agua' })
      .selectOption({ label: 'Water' });
    await check.click();
    await expect(preview.getByText('Correct.', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Close learner preview' }).click();

    const audioId = '6b64ed64-2c85-4f8b-a160-4d66bc012af1';
    const lessonRoute = `**/api/v1/manage/lessons/${id}`;
    await page.route(lessonRoute, async (route) => {
      const response = await route.fetch();
      const lesson = await response.json();
      lesson.exercises[0].audioMediaId = audioId;
      await route.fulfill({ response, json: lesson });
    });
    await page.route(`**/api/v1/manage/media/${audioId}/preview`, (route) =>
      route.fulfill({ json: { url: '/preview-fixture.wav', expiresInSeconds: 60 } }),
    );
    await page.route('**/preview-fixture.wav', (route) =>
      route.fulfill({ contentType: 'audio/wav', body: previewRecording() }),
    );
    await page.getByRole('button', { name: `${title} Version 1 · Draft`, exact: true }).click();
    await page.getByRole('button', { name: 'Preview as learner' }).click();
    await preview.getByRole('button', { name: 'Listen to exercise' }).click();
    const player = preview.locator('audio');
    await player.evaluate((element) => (element as HTMLAudioElement).play());
    await expect
      .poll(() => player.evaluate((element) => (element as HTMLAudioElement).currentTime))
      .toBeGreaterThan(0);
    await page.getByRole('button', { name: 'Close learner preview' }).click();
    await expect(player).toHaveCount(0);
    await page.unroute(lessonRoute);
    const copyTitle = `${title} copy`;
    await page.getByText('Duplicate draft', { exact: true }).click();
    await page.getByLabel('New lesson title').fill(copyTitle);
    const [copied] = await Promise.all([
      page.waitForResponse((response) =>
        response.url().endsWith(`/manage/lessons/${id}/duplicate`),
      ),
      page.getByRole('button', { name: 'Create draft copy' }).click(),
    ]);
    expect(copied.status()).toBe(201);
    const copy: { id: string } = await copied.json();
    expect(copy.id).not.toBe(id);
    expect(await copied.headerValue('location')).toMatch(
      new RegExp(`/api/v1/manage/lessons/${copy.id}$`),
    );
    await expect(page.getByRole('heading', { name: copyTitle, exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Exercises · 3' })).toBeVisible();
    expect((await request.get(`/api/v1/lessons/${copy.id}`)).status()).toBe(404);
    const withAudio = process.env['LEXIGO_E2E_PRIVATE_AUDIO'] === 'true';
    const source = `Workshop recording ${testInfo.project.name}`;
    if (withAudio) {
      await page.goto('/manage/media');
      await page.getByLabel('Audio file').setInputFiles({
        name: 'workshop.wav',
        mimeType: 'audio/wav',
        buffer: previewRecording(),
      });
      await page.getByLabel('Source').fill(source);
      await page.getByLabel('License or permission').fill('Generated solely for the local test.');
      await page.getByRole('button', { name: /Upload for independent review/ }).click();
      await expect(page.getByText('Audio uploaded.', { exact: false })).toBeVisible();
      await signInStaff(reviewer, request, baseURL!, inboxPort!, database!, ['Reviewer']);
      await reviewer.goto('/manage/media');
      const recording = reviewer.getByRole('article').filter({ hasText: source });
      await recording.getByRole('button', { name: 'Review recording' }).click();
      await recording
        .getByLabel('Review notes')
        .fill('Approved for this disposable browser walkthrough.');
      await recording.getByRole('button', { name: 'Approve audio' }).click();
      await expect(recording.getByText('Reviewed & ready')).toBeVisible();
      await openWorkshop(page);
      await chooseLesson(page, copyTitle);
      await page.getByText('Add a draft exercise', { exact: true }).click();
      await page.getByLabel('Exercise type').selectOption('Listening');
      await page.getByRole('button', { name: new RegExp(source) }).click();
      await page.getByLabel('English instruction or question').fill('Listen to the recording.');
      await page.getByLabel('Feedback explanation in English').fill('This is a test recording.');
      await page.getByLabel('Accepted answers, one per line').fill('Tone');
      await page.getByRole('button', { name: /Add to draft/ }).click();
      await expect(page.getByRole('heading', { name: 'Exercises · 4' })).toBeVisible();
      await page.getByRole('button', { name: 'Preview as learner' }).click();
      for (let index = 0; index < 3; index++) {
        await preview.getByRole('button', { name: 'Next exercise' }).click();
      }
      await preview.getByRole('button', { name: 'Listen to exercise' }).click();
      const realPlayer = preview.locator('audio');
      await realPlayer.evaluate((element) => (element as HTMLAudioElement).play());
      await expect
        .poll(() => realPlayer.evaluate((element) => (element as HTMLAudioElement).currentTime))
        .toBeGreaterThan(0);
      await page.getByRole('button', { name: 'Close learner preview' }).click();
      await expect(realPlayer).toHaveCount(0);
    }
    await page.getByRole('button', { name: `${title} Version 1 · Draft`, exact: true }).click();
    for (const item of [
      { type: 'FillBlank', prompt: 'Fill the gap: Yo bebo ____.', answer: 'agua', count: 4 },
      { type: 'Translation', prompt: 'Write agua in English.', answer: 'Water', count: 5 },
    ]) {
      if (!(await page.getByLabel('Exercise type').isVisible())) {
        await page.getByText('Add a draft exercise', { exact: true }).click();
      }
      await page.getByLabel('Exercise type').selectOption(item.type);
      await page.getByLabel('English instruction or question').fill(item.prompt);
      await page.getByLabel('Feedback explanation in English').fill('Agua means water.');
      await page.getByLabel('Accepted answers, one per line').fill(item.answer);
      await page.getByRole('button', { name: /Add to draft/ }).click();
      await expect(page.getByRole('heading', { name: `Exercises · ${item.count}` })).toBeVisible();
    }
    await page.getByText('Add a draft exercise', { exact: true }).click();
    await page.getByLabel('Exercise type').selectOption('Reading');
    await page.getByLabel('Reading passage').fill('Ana bebe agua. Luis bebe leche.');
    await page.getByLabel('English instruction or question').fill('Who drinks water?');
    await page.getByLabel('Feedback explanation in English').fill('Ana drinks water.');
    await page.getByLabel('Options, one per line').fill('Ana\nLuis');
    await page.getByLabel('Accepted answers, one per line').fill('Ana');
    await page.getByRole('button', { name: /Add to draft/ }).click();
    await expect(page.getByRole('heading', { name: 'Exercises · 6' })).toBeVisible();
    if (withAudio) {
      await page.getByText('Add a draft exercise', { exact: true }).click();
      await page.getByLabel('Exercise type').selectOption('Listening');
      await page.getByRole('button', { name: new RegExp(source) }).click();
      await page.getByLabel('English instruction or question').fill('Listen to the recording.');
      await page.getByLabel('Feedback explanation in English').fill('This is a sample test tone.');
      await page.getByLabel('Accepted answers, one per line').fill('Tone');
      await page.getByRole('button', { name: /Add to draft/ }).click();
      await expect(page.getByRole('heading', { name: 'Exercises · 7' })).toBeVisible();
    }
    await page.getByRole('button', { name: /Submit for independent review/ }).click();
    await expect(page.getByText(/VERSION 1 · Submitted/i).last()).toBeVisible();
    await expect(page.getByRole('button', { name: /Approve version/ })).toHaveCount(0);

    if (process.env['LEXIGO_E2E_PRIVATE_AUDIO'] !== 'true') {
      await signInStaff(reviewer, request, baseURL!, inboxPort!, database!, ['Reviewer']);
    }
    await openWorkshop(reviewer);
    await chooseLesson(reviewer, title);
    await reviewer
      .getByLabel('Review notes')
      .fill('The greeting and its accepted answer are correct.');
    await reviewer.getByRole('button', { name: /Approve version/ }).click();
    await expect(reviewer.getByText(/VERSION 1 · Approved/i).last()).toBeVisible();
    expect((await request.get(publicLesson)).status()).toBe(404);

    const [publisherRegistration] = await Promise.all([
      publisher.waitForRequest(
        (sent) =>
          sent.method() === 'POST' && new URL(sent.url()).pathname === '/api/v1/auth/register',
      ),
      signInStaff(publisher, request, baseURL!, inboxPort!, database!, ['Admin']),
    ]);
    const publisherEmail: string = publisherRegistration.postDataJSON().email;
    await openWorkshop(publisher);
    await chooseLesson(publisher, title);
    const publication = publisher.waitForResponse(
      (response) =>
        response.url().endsWith(`/manage/lessons/${id}/publish`) &&
        response.request().method() === 'POST',
    );
    await publisher.getByRole('button', { name: /Publish reviewed version/ }).click();
    expect((await publication).status()).toBe(200);
    await expect(publisher.getByText(/VERSION 1 · Published/i).last()).toBeVisible();
    const published = await request.get(publicLesson);
    expect(published.status()).toBe(200);
    expect(await published.json()).toMatchObject({ title });

    const learner = await browser.newPage(staffPageOptions);
    try {
      await registerLearner(learner, request, baseURL!, inboxPort!);
      await completePublishedExercises(learner, title, withAudio);
    } finally {
      await learner.close();
    }

    await openWorkshop(publisher);
    await chooseLesson(publisher, title);
    await publisher.getByText('Change lesson position', { exact: true }).click();
    const lessonPosition = publisher.getByLabel('Position in unit');
    const newPosition = testInfo.project.name === 'phone' ? '9890' : '9891';
    await lessonPosition.fill(newPosition);
    const [lessonMoved] = await Promise.all([
      publisher.waitForResponse(
        (response) =>
          response.request().method() === 'PUT' &&
          response.url().endsWith(`/manage/lessons/${id}/position`),
      ),
      publisher.getByRole('button', { name: 'Save lesson position' }).click(),
    ]);
    expect(lessonMoved.ok()).toBe(true);
    await expect(publisher.getByText('Your change is saved.', { exact: true })).toBeVisible();
    expect(await (await request.get(publicLesson)).json()).toMatchObject({
      orderNumber: Number(newPosition),
    });

    await publisher.getByText('Archive published lesson', { exact: true }).click();
    await expect(publisher.getByRole('button', { name: 'Confirm archive' })).toBeDisabled();
    await publisher
      .getByLabel('Reason for archiving')
      .fill('Completed the publishing walkthrough.');
    const [archived] = await Promise.all([
      publisher.waitForResponse(
        (response) =>
          response.url().endsWith(`/manage/lessons/${id}`) &&
          response.request().method() === 'DELETE',
      ),
      publisher.getByRole('button', { name: 'Confirm archive' }).click(),
    ]);
    expect(archived.ok()).toBe(true);
    const editor = publisher.getByRole('region', { name: 'Lesson editor' });
    await expect(editor.getByRole('heading', { name: title, exact: true })).toBeVisible();
    await expect(editor.getByText('VERSION 1 · Archived', { exact: true })).toBeVisible();
    expect((await request.get(publicLesson)).status()).toBe(404);

    await publisher.getByText('Manage course units', { exact: true }).click();
    const unitTitle = `Workshop unit ${testInfo.project.name}`;
    await publisher.getByLabel('Unit title').fill(unitTitle);
    await publisher.getByLabel('English description').fill('First draft of the unit description.');
    await publisher
      .locator('#unit-order')
      .fill(testInfo.project.name === 'phone' ? '9898' : '9899');
    const [unitCreated] = await Promise.all([
      publisher.waitForResponse(
        (response) =>
          response.request().method() === 'POST' &&
          new URL(response.url()).pathname === '/api/v1/manage/units',
      ),
      publisher.getByRole('button', { name: /Create unit/ }).click(),
    ]);
    expect(unitCreated.status()).toBe(201);
    expect(await unitCreated.headerValue('location')).toMatch(
      /\/api\/v1\/manage\/units\/[a-f0-9-]{36}$/,
    );
    const unitPicker = publisher.getByLabel('Unit to edit');
    await unitPicker.selectOption({ label: unitTitle });
    await expect(publisher.getByLabel('English description')).toHaveValue(
      'First draft of the unit description.',
    );
    await publisher.getByLabel('English description').fill('Revised unit description.');
    await publisher.getByLabel('Difficulty', { exact: true }).selectOption('A2');
    const [unitSaved] = await Promise.all([
      publisher.waitForResponse(
        (response) =>
          response.request().method() === 'PUT' &&
          /\/manage\/units\/[^/]+$/.test(new URL(response.url()).pathname),
      ),
      publisher.getByRole('button', { name: /Save unit changes/ }).click(),
    ]);
    expect(unitSaved.ok()).toBe(true);
    await unitPicker.selectOption({ label: unitTitle });
    await expect(publisher.getByLabel('English description')).toHaveValue(
      'Revised unit description.',
    );
    await expect(publisher.getByLabel('Difficulty', { exact: true })).toHaveValue('A2');
    await publisher.getByText('Change unit position', { exact: true }).click();
    const unitPosition = publisher.getByLabel('Position in course');
    const movedUnitPosition = testInfo.project.name === 'phone' ? '9888' : '9889';
    await unitPosition.fill(movedUnitPosition);
    const [unitMoved] = await Promise.all([
      publisher.waitForResponse(
        (response) =>
          response.request().method() === 'PUT' &&
          /\/manage\/units\/[^/]+\/position$/.test(new URL(response.url()).pathname),
      ),
      publisher.getByRole('button', { name: 'Save unit position' }).click(),
    ]);
    expect(unitMoved.ok()).toBe(true);
    await expect(publisher.getByText('Your change is saved.', { exact: true })).toBeVisible();
    await expect(publisher.locator('#unit-order')).toHaveValue(movedUnitPosition);
    await publisher.getByText('Archive unit', { exact: true }).click();
    const unitReason = publisher.getByLabel('Reason for changing unit visibility');
    await expect(publisher.getByRole('button', { name: 'Confirm unit archive' })).toBeDisabled();
    await unitReason.fill('Temporary review of this unit.');
    await publisher.getByRole('button', { name: 'Confirm unit archive' }).click();
    await expect(publisher.getByText('Restore unit', { exact: true })).toBeVisible();
    await expect(publisher.getByText('Your change is saved.', { exact: true })).toBeVisible();
    if (!(await unitReason.isVisible())) {
      await publisher.getByText('Restore unit', { exact: true }).click();
    }
    await unitReason.fill('The unit review is complete.');
    await publisher.getByRole('button', { name: 'Confirm unit restore' }).click();
    await expect(publisher.getByText('Archive unit', { exact: true })).toBeVisible();
    await expect(publisher.getByText('Your change is saved.', { exact: true })).toBeVisible();

    const courseName = testInfo.project.name === 'phone' ? 'Italian' : 'German';
    await publisher.getByRole('button', { name: courseName, exact: true }).click();
    await expect(publisher.getByText('Opening the workshop…')).toBeHidden();
    await publisher.getByText('Edit course details', { exact: true }).click();
    const revisedDescription = `Updated ${courseName} course description in the workshop test.`;
    await publisher.getByLabel('Course description').fill(revisedDescription);
    const [courseEdited] = await Promise.all([
      publisher.waitForResponse(
        (response) =>
          response.request().method() === 'PUT' &&
          /\/manage\/courses\/[^/]+$/.test(new URL(response.url()).pathname),
      ),
      publisher.getByRole('button', { name: 'Save course details' }).click(),
    ]);
    expect(courseEdited.ok()).toBe(true);
    await expect(publisher.getByText('Your change is saved.', { exact: true })).toBeVisible();
    await publisher.getByText('Archive course', { exact: true }).click();
    const reason = publisher.getByLabel('Reason for changing course visibility');
    await expect(publisher.getByRole('button', { name: 'Confirm course archive' })).toBeDisabled();
    await reason.fill('Temporary course review in the workshop test.');
    await publisher.getByRole('button', { name: 'Confirm course archive' }).click();
    await expect(publisher.getByText('Restore course', { exact: true })).toBeVisible();
    await expect(publisher.getByText('Your change is saved.', { exact: true })).toBeVisible();
    if (!(await reason.isVisible())) {
      await publisher.getByText('Restore course', { exact: true }).click();
    }
    await reason.fill('Course review finished in the workshop test.');
    await publisher.getByRole('button', { name: 'Confirm course restore' }).click();
    await expect(publisher.getByText('Archive course', { exact: true })).toBeVisible();
    await expect(publisher.getByText('Your change is saved.', { exact: true })).toBeVisible();
    await publisher.getByText('Change course position', { exact: true }).click();
    const catalogPosition = publisher.getByLabel('Position in catalog');
    await catalogPosition.fill(testInfo.project.name === 'phone' ? '9898' : '9899');
    const [courseMoved] = await Promise.all([
      publisher.waitForResponse(
        (response) =>
          response.request().method() === 'PUT' &&
          /\/manage\/courses\/[^/]+\/position$/.test(new URL(response.url()).pathname),
      ),
      publisher.getByRole('button', { name: 'Save course position' }).click(),
    ]);
    expect(courseMoved.ok()).toBe(true);
    await expect(publisher.getByText('Your change is saved.', { exact: true })).toBeVisible();
    const catalogCourses: { title: string }[] = await (await request.get('/api/v1/courses')).json();
    const titles = catalogCourses.map((item) => item.title);
    expect(titles).toContain('Russian');
    expect(titles.indexOf(courseName)).toBeGreaterThan(titles.indexOf('Russian'));
    await publisher.goto('/courses');
    const cards = publisher.getByRole('region', { name: 'Published courses' }).getByRole('heading');
    await expect(cards.filter({ hasText: courseName })).toBeVisible();
    await expect(publisher.getByText(revisedDescription, { exact: true })).toBeVisible();
    const displayedTitles = await cards.allTextContents();
    expect(displayedTitles.indexOf(courseName)).toBeGreaterThan(displayedTitles.indexOf('Russian'));
    await openWorkshop(publisher);
    await chooseLesson(publisher, copyTitle);
    await publisher.getByText('Hand over this unpublished lesson', { exact: true }).click();
    const assignOwner = publisher.getByRole('button', { name: 'Assign lesson owner' });
    await expect(assignOwner).toBeDisabled();
    await publisher.getByLabel('Receiving author’s email').fill(publisherEmail);
    await publisher
      .getByLabel('Handover reason')
      .fill('Retain the shared draft under a continuing author.');
    await publisher.getByRole('checkbox', { name: /I reviewed this shared draft/ }).check();
    const [handover] = await Promise.all([
      publisher.waitForResponse(
        (response) => response.request().method() === 'POST' && response.url().endsWith('/owner'),
      ),
      assignOwner.click(),
    ]);
    expect(handover.status()).toBe(200);
    await expect(publisher.getByText('The lesson has a new owner.', { exact: true })).toBeVisible();
  } finally {
    await Promise.all([reviewer.close(), publisher.close()]);
  }
});
