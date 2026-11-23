import { execFileSync } from 'node:child_process';
import { expect, test } from '@playwright/test';
import { enterCredentials, registerLearner } from './test-account';
import { signInStaff } from './staff-account';
import { buySimulatedPremium } from './simulated-premium';

const inboxPort = process.env['LEXIGO_E2E_INBOX_PORT'];
const database = process.env['LEXIGO_E2E_DATABASE'];
const spanish = '10000000-0000-0000-0000-000000000001';

test('a new learner confirms their email and completes their first lesson', async ({
  page,
  request,
  baseURL,
}) => {
  test.skip(!inboxPort, 'Run with the disposable database and Mailpit inbox.');
  test.setTimeout(240_000);
  const project = test.info().project.name;
  const displayName = `Browser Learner ${project}`;
  const { email, password } = await registerLearner(
    page,
    request,
    baseURL!,
    inboxPort!,
    displayName,
  );
  await page.goto('/account');
  await expect(page.getByText(displayName, { exact: true })).toBeVisible();
  await expect(page.getByText('Confirmed ✓', { exact: true })).toBeVisible();

  await page.goto(`/learn/${spanish}`);
  await page.getByRole('link', { name: /Choose my goal and starting point/ }).click();
  await page.getByRole('button', { name: /20 XP \/ day/ }).click();
  await page.getByRole('button', { name: /Continue/ }).click();
  await page.getByRole('button', { name: /Start from scratch/ }).click();
  const [lessonStarted] = await Promise.all([
    page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        /\/api\/v1\/lessons\/[^/]+\/sessions$/.test(new URL(response.url()).pathname),
    ),
    page.getByRole('button', { name: /Let’s begin/ }).click(),
  ]);
  expect(lessonStarted.status()).toBe(201);
  expect(await lessonStarted.headerValue('location')).toMatch(
    /\/api\/v1\/me\/learning-sessions\/[a-f0-9-]{36}$/,
  );
  await expect(page).toHaveURL(/\/learn\/session\//);
  const firstLessonUrl = page.url();
  await page.goto(`/learn/${spanish}`);
  const locked = page.locator('li.locked').filter({ hasText: 'Meet the people' }).first();
  await locked.locator('summary').click();
  await expect(locked.getByRole('button', { name: /Start lesson/ })).toBeDisabled();
  await locked.getByRole('link', { name: 'Your first drinks', exact: true }).click();
  await expect(page.locator('summary').filter({ hasText: /^Your first drinks$/ })).toBeFocused();
  await page.goto(firstLessonUrl);
  const character = page.locator('app-character-cue svg');
  await expect(character).toHaveAttribute('data-mood', 'greeting');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await expect(character.locator('.right-arm')).toHaveCSS('animation-name', 'none');
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await expect(character.locator('.right-arm')).toHaveCSS('animation-name', /(?:^|_)wave$/);
  await page.getByRole('button', { name: 'Pause movement' }).press('Space');
  await expect(character.locator('.right-arm')).toHaveCSS('animation-play-state', 'paused');
  await page.getByRole('button', { name: 'Resume movement' }).press('Space');
  await expect(character.locator('.right-arm')).toHaveCSS('animation-play-state', 'running');

  await page.getByRole('link', { name: 'Report an issue' }).click();
  const reportComment = `Please review this practice wording (${project}).`;
  await page.getByLabel('Issue', { exact: true }).selectOption('Text');
  await page.getByLabel('What happened?').fill(reportComment);
  const [createdReport] = await Promise.all([
    page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname === '/api/v1/reports',
    ),
    page.getByRole('button', { name: 'Send report' }).click(),
  ]);
  expect(createdReport.status()).toBe(201);
  expect(await createdReport.headerValue('location')).toMatch(
    /\/api\/v1\/me\/reports\/[a-f0-9-]{36}$/,
  );
  await expect(page.getByText(reportComment, { exact: true })).toBeVisible();
  await page.getByRole('link', { name: /Back to lesson/ }).click();
  await expect(page).toHaveURL(firstLessonUrl);

  const vocabulary = [
    { english: 'coffee', spanish: 'café' },
    { english: 'milk', spanish: 'leche' },
    { english: 'tea', spanish: 'té' },
    { english: 'water', spanish: 'agua' },
  ];
  const checkAnswer = async () => {
    await page.getByRole('button', { name: 'Check', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Nice!' })).toBeVisible();
    if (await character.isVisible())
      await expect(character).toHaveAttribute('data-mood', 'correct');
    await page.getByRole('button', { name: /Continue/ }).click();
  };
  for (const word of vocabulary) {
    await page.getByRole('radio', { name: new RegExp(` ${word.spanish}$`) }).press('Space');
    await checkAnswer();

    await expect(page.getByRole('heading', { name: 'Write this in English' })).toBeVisible();
    await expect(character).toHaveAttribute('data-mood', 'idle');
    await expect(page.getByRole('button', { name: word.spanish, exact: true })).toBeVisible();
    await page.getByRole('button', { name: new RegExp(`^Add ${word.english}, tile`) }).click();
    await checkAnswer();
    const translation = page.getByLabel('Your translation');
    if (word.english === 'coffee') {
      await translation.fill('caf');
      await page.getByText('Need a letter? Open the language keyboard').click();
      await page.getByRole('button', { name: 'Insert é', exact: true }).click();
      await expect(translation).toHaveValue('café');
    } else {
      await translation.fill(word.english === 'water' ? 'incorrect' : word.spanish);
    }
    if (word.english === 'water') {
      await page.getByRole('button', { name: 'Check', exact: true }).click();
      const feedback = page.locator('app-answer-feedback-panel');
      await expect(feedback.getByRole('heading', { name: 'Incorrect' })).toBeVisible();
      await expect(character).toHaveAttribute('data-mood', 'encouragement');
      await expect(feedback.locator('.answer-correction')).toContainText('Correct answeragua');
      await page.reload();
      await expect(feedback.getByRole('heading', { name: 'Incorrect' })).toBeVisible();
      await expect(feedback.locator('.answer-correction strong')).toHaveText('agua');
      await page.getByRole('button', { name: /Continue/ }).click();
    } else {
      await checkAnswer();
    }
  }

  await page.getByRole('button', { name: /Finish & see results/ }).click();
  await expect(character).toHaveAttribute('data-mood', 'celebration');
  await expect(page.getByText('91%', { exact: true })).toBeVisible();
  await expect(page.getByText('+20', { exact: true })).toBeVisible();
  await expect(page.locator('app-lesson-result-stats').getByText('11 / 12')).toBeVisible();
  await expect(
    page.locator('app-lesson-result-stats').getByText('Time', { exact: true }),
  ).toBeVisible();
  await page.getByText('Your answers & explanations').click();
  await expect(page.getByText('Correct answer: agua')).toBeVisible();
  await expect(page.getByRole('button', { name: /Try this lesson again/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Start next lesson/ })).toBeVisible();
  await expect(
    page.getByRole('button', { name: /Free practice · no hearts needed/ }),
  ).toBeVisible();
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Look how far you’ve come.' })).toBeVisible();
  await expect(page.getByText('+20', { exact: true })).toBeVisible();
  await page.goto(`/learn/${spanish}`);
  await expect(page.getByText(/1 of \d+ learning steps completed/)).toBeVisible();
  await page.goto('/');
  const hero = page.locator('app-home-hero');
  await expect(hero.getByRole('link', { name: /Continue learning/ })).toBeVisible();
  await expect(hero.getByRole('link', { name: 'I already have an account' })).toHaveCount(0);
  await page.reload();
  await expect(hero.getByRole('link', { name: /Continue learning/ })).toBeVisible();
  await hero.getByRole('link', { name: 'My account', exact: true }).click();
  await page.getByRole('button', { name: 'Sign out ↗', exact: true }).click();
  await expect(page).toHaveURL(/\/login(?:\?|$)/);
  await page.goto('/');
  await expect(hero.getByRole('link', { name: 'I already have an account' })).toBeVisible();
  await hero.getByRole('link', { name: 'I already have an account' }).click();
  await enterCredentials(page, email, password);
  await expect(page).not.toHaveURL(/\/login(?:\?|$)/);
  await page.goto(`/learn/${spanish}`);
  await expect(page.getByText(/1 of \d+ learning steps completed/)).toBeVisible();
  await page.goto(firstLessonUrl);
  await expect(page.getByText('+20', { exact: true })).toBeVisible();
  await expect(page.locator('app-lesson-result-stats').getByText('11 / 12')).toBeVisible();
  const italian = '10000000-0000-0000-0000-000000000002';
  await page.goto(`/learn/${italian}`);
  await page.getByRole('link', { name: /Choose my goal and starting point/ }).click();
  await page.getByRole('button', { name: /20 XP \/ day/ }).click();
  await page.getByRole('button', { name: /Continue/ }).click();
  await page.getByRole('button', { name: /Start from scratch/ }).click();
  await page.getByRole('button', { name: /Let’s begin/ }).click();
  await expect(page).toHaveURL(/\/learn\/session\//);
  await page.goto(`/learn/${italian}`);
  await expect(page.getByText(/0 of \d+ learning steps completed/)).toBeVisible();

  await page.goto(`/leaderboard?courseId=${spanish}`);
  const rankingCourse = page.getByLabel('Your course');
  const ranking = page.getByRole('list', { name: 'Weekly course ranking' });
  const ownEntry = ranking
    .getByRole('listitem')
    .filter({ has: page.getByText('You', { exact: true }) });
  const joinRanking = page.getByRole('button', { name: 'Join weekly ranking' });
  await expect(rankingCourse).toHaveValue(spanish);
  await expect(page.getByText('Your XP this week: 20')).toBeVisible();
  await expect(ownEntry).toHaveCount(0);
  await joinRanking.click();
  await expect(ownEntry.getByText('You', { exact: true })).toBeVisible();
  await expect(ownEntry.locator('.avatar')).toHaveText('BL');
  await expect(ownEntry.getByText(email)).toHaveCount(0);

  if (process.env['LEXIGO_E2E_PRIVATE_AUDIO'] === 'true') {
    await page.goto('/account/profile');
    const photo = await page.evaluate(() => {
      const canvas = document.createElement('canvas');
      canvas.width = canvas.height = 16;
      const context = canvas.getContext('2d')!;
      context.fillStyle = '#25798b';
      context.fillRect(0, 0, 16, 16);
      return canvas.toDataURL('image/png').split(',')[1]!;
    });
    await page.getByLabel('Choose photo').setInputFiles({
      name: 'ranking-photo.png',
      mimeType: 'image/png',
      buffer: Buffer.from(photo, 'base64'),
    });
    await page.getByRole('button', { name: 'Save photo' }).click();
    await expect(page.getByText('Profile photo saved.')).toBeVisible();
    await page.goto(`/leaderboard?courseId=${spanish}`);
    await expect
      .poll(() => ownEntry.locator('img').evaluate((image: HTMLImageElement) => image.naturalWidth))
      .toBe(128);
    await page.getByRole('button', { name: 'Share profile photo' }).click();
    const stopSharing = page.getByRole('button', { name: 'Stop sharing profile photo' });
    await expect(stopSharing).toBeVisible();
    await page.reload();
    await expect(stopSharing).toBeVisible();
    await stopSharing.click();
    await expect(page.getByRole('button', { name: 'Share profile photo' })).toBeVisible();
  }

  await page.getByRole('button', { name: /Previous week/ }).click();
  await expect(page.getByText('Your XP this week: 0')).toBeVisible();
  await expect(ownEntry).toHaveCount(0);
  await page.getByRole('button', { name: 'This week' }).click();
  await expect(ownEntry).toBeVisible();
  await rankingCourse.selectOption(italian);
  await expect(page.getByText('Your XP this week: 0')).toBeVisible();
  await expect(ownEntry).toHaveCount(0);
  await rankingCourse.selectOption(spanish);
  await expect(ownEntry).toBeVisible();

  await page.getByRole('button', { name: 'Leave ranking' }).click();
  await expect(joinRanking).toBeVisible();
  await expect(ownEntry).toHaveCount(0);
  await page.reload();
  await expect(joinRanking).toBeVisible();
  await expect(page.getByText('Your XP this week: 20')).toBeVisible();
  await expect(ownEntry).toHaveCount(0);

  await page.goto(`/learn/${italian}`);
  await page.getByRole('link', { name: /All your languages/ }).click();
  await page.getByRole('link', { name: /Spanish.*Open your chapters/ }).click();
  await expect(page.getByText(/1 of \d+ learning steps completed/)).toBeVisible();
  await expect(
    page.getByRole('button', { name: 'Start Meet the people', exact: true }),
  ).toBeEnabled();
  await expect(
    page.getByRole('button', { name: 'Review Your first drinks', exact: true }),
  ).toBeEnabled();
  await page.getByText('Practice and placement tools', { exact: true }).click();
  const skills = page.getByRole('region', { name: 'Your skills map' });
  await expect(skills.getByText('Not yet practised').first()).toBeVisible();
  await expect(skills.getByText(/correct · 8 answers/)).toBeVisible();
  await expect(page.getByText(/Needs another try.*Missed/)).toBeVisible();
  const mistake = page.locator('app-mistake-practice li').filter({ hasText: 'Needs another try' });
  await expect(mistake.getByText('Vocabulary', { exact: true })).toBeVisible();
  await expect(mistake.getByText(/Last attempted/)).toBeVisible();
  const mistakeRoute = '**/api/v1/me/mistakes?*';
  await page.route(mistakeRoute, async (route) => {
    const pageNumber = Number(new URL(route.request().url()).searchParams.get('page') ?? '1');
    await route.fulfill({
      json: {
        page: pageNumber,
        pageSize: 20,
        totalCount: 21,
        items: Array.from({ length: pageNumber === 1 ? 20 : 1 }, (_, index) => ({
          exerciseId: `question-${pageNumber}-${index}`,
          prompt: `Review question ${pageNumber}-${index}`,
          type: 'Translation',
          contextText: null,
          wrongCount: 1,
          lastMissedAt: null,
        })),
      },
    });
  });
  await page.reload();
  await page.getByText('Practice and placement tools', { exact: true }).click();
  await page.getByRole('button', { name: 'Show more review questions' }).click();
  const reviewPages = page.getByRole('navigation', { name: 'Review question pages' });
  const nextPage = reviewPages.getByRole('button', { name: 'Next' });
  await nextPage.press('Enter');
  await expect(page.getByText('Review question 2-0')).toBeVisible();
  await expect(nextPage).toBeFocused();
  await expect(nextPage).toHaveAttribute('aria-disabled', 'true');
  await reviewPages.getByRole('button', { name: 'Previous' }).press('Enter');
  await expect(page.getByText('Review question 1-19')).toBeVisible();
  await page.unroute(mistakeRoute);
  await page.reload();
  await page.getByText('Practice and placement tools', { exact: true }).click();
  await page.getByRole('button', { name: 'Review Vocabulary mistakes' }).click();
  await expect(page).toHaveURL(/\/learn\/session\//);
  await expect(page.getByRole('heading', { name: 'Vocabulary mistake practice' })).toBeVisible();
  await page.getByRole('button', { name: /Now try it/ }).click();
  await expect(page.getByLabel('Your translation')).toBeVisible();
  await page.getByLabel('Your translation').fill('incorrect');
  await page.getByRole('button', { name: 'Check', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Incorrect' })).toBeVisible();
  await page.getByRole('button', { name: /Continue/ }).click();
  await page.getByRole('button', { name: /Finish & see results/ }).click();
  await expect(page.getByText('0%', { exact: true })).toBeVisible();

  await page.goto(`/learn/${spanish}`);
  await page.getByText('Practice and placement tools', { exact: true }).click();
  await expect(
    page.locator('app-mistake-practice').getByRole('link', { name: /See Premium plans/ }),
  ).toBeVisible();
  await buySimulatedPremium(page);
  await page.goto(`/learn/${spanish}`);
  await page.getByText('Practice and placement tools', { exact: true }).click();
  await expect(page.getByText(/Repeated mistake.*Missed 2 times/)).toBeVisible();
  await page.getByRole('button', { name: 'Due for review', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveText('No questions are due for review yet.');
  const [practiceStarted] = await Promise.all([
    page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname === '/api/v1/me/practice-sessions',
    ),
    page.getByRole('button', { name: 'Repeated mistakes', exact: true }).click(),
  ]);
  expect(practiceStarted.status()).toBe(201);
  expect(await practiceStarted.headerValue('location')).toMatch(
    /\/api\/v1\/me\/learning-sessions\/[a-f0-9-]{36}$/,
  );
  await expect(page.getByRole('heading', { name: 'Repeated mistakes', exact: true })).toBeVisible();
  await page.getByRole('button', { name: /Now try it/ }).click();
  await page.getByLabel('Your translation').fill('agua');
  await checkAnswer();
  await page.getByRole('button', { name: /Finish & see results/ }).click();
  await expect(page.getByText('100%', { exact: true })).toBeVisible();
  if (database && /^lexigo-browser-db-[a-f0-9]{32}$/.test(database)) {
    const sessionId = new URL(page.url()).pathname.split('/').at(-1);
    expect(sessionId).toMatch(/^[a-f0-9-]{36}$/);
    // Advance only the reviewed exercise's history in the disposable database.
    execFileSync(
      'docker',
      [
        'exec',
        '-i',
        database,
        'psql',
        '-Xq',
        '-v',
        'ON_ERROR_STOP=1',
        '-U',
        'postgres',
        '-d',
        'postgres',
      ],
      {
        input: `UPDATE "AnswerAttempts" a SET "SubmittedAt" = a."SubmittedAt" - interval '4 days'
        FROM "SessionExercises" e, "SessionExercises" target
        WHERE target."SessionId" = '${sessionId}' AND e."UserId" = target."UserId"
          AND e."ExerciseId" = target."ExerciseId" AND a."SessionExerciseId" = e."Id"
          AND a."UserId" = e."UserId";`,
      },
    );
    await page.goto(`/learn/${spanish}`);
    await page.getByText('Practice and placement tools', { exact: true }).click();
    const practice = page.locator('app-mistake-practice');
    await expect(practice.locator('li').filter({ hasText: 'Due for review' })).toHaveCount(1);
    await practice.getByRole('button', { name: 'Due for review', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Due for review', exact: true })).toBeVisible();
    await page.getByRole('button', { name: /Now try it/ }).click();
    await page.getByLabel('Your translation').fill('agua');
    await checkAnswer();
    await page.getByRole('button', { name: /Finish & see results/ }).click();
    await expect(page.getByText('100%', { exact: true })).toBeVisible();
    await page.goto(`/learn/${spanish}`);
    await page.getByText('Practice and placement tools', { exact: true }).click();
    await practice.getByRole('button', { name: 'Due for review', exact: true }).click();
    await expect(practice.getByRole('alert')).toHaveText('No questions are due for review yet.');
  }
  await page.goto(`/learn/${spanish}`);
  await page.getByRole('link', { name: /My learning week/ }).click();
  await page.getByRole('button', { name: /Compare with the previous week/ }).click();
  const comparison = page.getByRole('region', { name: 'Week comparison' });
  await expect(comparison.getByRole('row', { name: 'XP earned 0 20' })).toBeVisible();
  const weeklyTotals = page.locator('dl.totals');
  await expect(
    weeklyTotals
      .getByText('First lesson completions', { exact: true })
      .locator('..')
      .getByText('1', { exact: true }),
  ).toBeVisible();
  await expect(
    weeklyTotals
      .getByText('XP earned', { exact: true })
      .locator('..')
      .getByText('20', { exact: true }),
  ).toBeVisible();
  await page.getByRole('link', { name: 'Completion cards', exact: true }).click();
  await expect(
    page.getByText('Complete every published lesson in a unit to earn its card.'),
  ).toBeVisible();
  await page.goto(`/learn/${spanish}`);
  await page.getByRole('link', { name: 'Learning activity', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Learning activity' })).toBeVisible();
  const activity = page.getByRole('list', { name: 'Recorded study days' });
  await expect(activity.getByText('20 XP', { exact: true })).toBeVisible();
  await expect(activity.getByText('1 lesson completed')).toBeVisible();
  await page.getByLabel('From').fill('2020-01-01');
  await page.getByLabel('To').fill('2020-01-31');
  await page.getByRole('button', { name: 'Show activity' }).click();
  await expect(page.getByRole('status')).toHaveText(
    'There’s no recorded learning activity for these dates.',
  );

  if (!database || !/^lexigo-browser-db-[a-f0-9]{32}$/.test(database)) return;
  expect(email).toMatch(/^learner-[a-f0-9-]+@example\.test$/);
  // Seed the other completed steps so this test can exercise the real card issuance and sharing UI.
  const unit = execFileSync(
    'docker',
    [
      'exec',
      '-i',
      database,
      'psql',
      '-XAtq',
      '-v',
      'ON_ERROR_STOP=1',
      '-U',
      'postgres',
      '-d',
      'postgres',
    ],
    {
      encoding: 'utf8',
      input: `
      INSERT INTO "LessonProgress" ("UserId", "LessonId", "Status", "BestScore", "CompletedAt")
      SELECT u."Id", l."Id", 'Completed', 100, now() - interval '1 minute'
      FROM "Users" u CROSS JOIN "Lessons" l JOIN "Units" unit ON unit."Id" = l."UnitId"
      WHERE u."Email" = '${email}' AND unit."CourseId" = '${spanish}'
        AND unit."Title" = 'Your first words' AND l."Status" = 'Published'
      ON CONFLICT DO NOTHING;
      SELECT "Id" FROM "Units" WHERE "CourseId" = '${spanish}' AND "Title" = 'Your first words';`,
    },
  ).trim();
  expect(unit).toMatch(/^[a-f0-9-]{36}$/);
  await page.goto(`/completion-cards?unitId=${unit}`);
  const [createdCard] = await Promise.all([
    page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname === `/api/v1/me/units/${unit}/completion-card`,
    ),
    page.getByRole('button', { name: 'Create my completed-unit card' }).click(),
  ]);
  expect(createdCard.status()).toBe(201);
  expect(await createdCard.headerValue('location')).toMatch(
    /\/api\/v1\/me\/completion-cards\/[a-f0-9-]{36}$/,
  );
  await expect(page.getByText('This card is private.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Enable public sharing' })).toBeDisabled();
  await page.getByRole('checkbox').check();
  await page.getByRole('button', { name: 'Enable public sharing' }).click();
  const publicLink = page.getByLabel('Public card link');
  await expect(publicLink).toHaveValue(/\/shared\/completion\//);
  const link = await publicLink.inputValue();
  await page.context().grantPermissions(['clipboard-read', 'clipboard-write'], {
    origin: new URL(link).origin,
  });
  await page.getByRole('button', { name: 'Copy public link' }).click();
  await expect(page.getByText('Public link copied.')).toBeVisible();
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(link);

  await page.evaluate(() => {
    window.print = () => document.documentElement.setAttribute('data-print-requested', 'true');
  });
  await page.getByRole('button', { name: 'Print this card' }).click();
  await expect(page.locator('html')).toHaveAttribute('data-print-requested', 'true');
  await page.emulateMedia({ media: 'print' });
  await expect(page.locator('app-completion-card-art')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Print this card' })).toBeHidden();
  await page.emulateMedia({ media: 'screen' });
  const visitor = await page
    .context()
    .browser()!
    .newPage({ baseURL, viewport: page.viewportSize() });
  const moderator = await page
    .context()
    .browser()!
    .newPage({ baseURL, viewport: page.viewportSize() });
  try {
    await visitor.goto(link);
    await expect(visitor.getByText(displayName, { exact: true })).toBeVisible();
    await registerLearner(visitor, request, baseURL!, inboxPort!, 'Report Learner');
    await visitor.goto(link);
    await visitor.getByRole('link', { name: 'Report the public name' }).click();
    const complaint = `Please review this public display name (${project}).`;
    await visitor.getByLabel('What happened?').fill(complaint);
    await visitor.getByRole('button', { name: 'Send report' }).click();
    await expect(visitor.getByText('Report received.')).toBeVisible();
    await signInStaff(moderator, request, baseURL!, inboxPort!, database!, ['Moderator']);
    await moderator.goto('/manage/reports');
    const report = moderator.getByRole('article').filter({ hasText: complaint });
    await report.getByRole('button', { name: 'Review report' }).click();
    await report.getByLabel('Status', { exact: true }).selectOption('Resolved');
    await report
      .getByLabel('Decision for the learner')
      .fill('The card was made private for review.');
    await report.getByRole('button', { name: 'Save review' }).click();
    await expect(moderator.getByText('Review saved.')).toBeVisible();
    await visitor.goto('/reports');
    await expect(visitor.getByText('The card was made private for review.')).toBeVisible();
    await page.reload();
    await expect(page.getByText('This card is private.')).toBeVisible();
    await visitor.goto(link);
    await expect(visitor.getByRole('heading', { name: 'This card is unavailable.' })).toBeVisible();

    await page.goto(`/leaderboard?courseId=${spanish}`);
    await page.getByRole('button', { name: 'Join weekly ranking' }).click();
    await expect(page.getByRole('button', { name: 'Leave ranking' })).toBeVisible();
    await visitor.goto(`/learn/${spanish}`);
    await visitor.getByRole('link', { name: /Choose my goal and starting point/ }).click();
    await visitor.getByRole('button', { name: /20 XP \/ day/ }).click();
    await visitor.getByRole('button', { name: /Continue/ }).click();
    await visitor.getByRole('button', { name: /Start from scratch/ }).click();
    await visitor.getByRole('button', { name: /Let’s begin/ }).click();
    await expect(visitor).toHaveURL(/\/learn\/session\//);
    await visitor.goto(`/leaderboard?courseId=${spanish}`);
    await visitor
      .getByRole('link', { name: `Report public name ${displayName}`, exact: true })
      .click();
    const rankingComplaint = `Please review this ranking name (${project}).`;
    await visitor.getByLabel('What happened?').fill(rankingComplaint);
    await visitor.getByRole('button', { name: 'Send report' }).click();
    await expect(visitor.getByText('Report received.')).toBeVisible();
    await moderator.goto('/manage/reports');
    const rankingReport = moderator.getByRole('article').filter({ hasText: rankingComplaint });
    await rankingReport.getByRole('button', { name: 'Review report' }).click();
    await rankingReport.getByLabel('Status', { exact: true }).selectOption('Resolved');
    await rankingReport
      .getByLabel('Decision for the learner')
      .fill('Ranking participation paused for name review.');
    await rankingReport.getByRole('button', { name: 'Save review' }).click();
    await expect(moderator.getByText('Review saved.')).toBeVisible();
    await page.reload();
    await expect(page.getByRole('button', { name: 'Join weekly ranking' })).toBeVisible();
    await expect(page.getByText('Your XP this week: 20')).toBeVisible();
  } finally {
    await visitor.close();
    await moderator.close();
  }
});
