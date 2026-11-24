import { expect, test } from '@playwright/test';

for (const course of [
  { name: 'Spanish', title: 'Articles, identity and location', sample: 'Estoy en casa.' },
  { name: 'Italian', title: 'Articles, agreement and everyday verbs', sample: 'Parlo italiano.' },
  { name: 'German', title: 'Articles, word order and cases', sample: 'Ich spreche mit dem Mann.' },
  { name: 'Russian', title: 'Cyrillic, gender and first cases', sample: 'Я читаю книгу.' },
]) {
  test(`${course.name} exposes teach-first grammar notes and its unit check`, async ({ page }) => {
    await page.goto('/courses');
    await page.getByRole('button', { name: new RegExp(course.name) }).click();
    await page
      .getByRole('navigation', { name: 'Course units' })
      .getByRole('button', { name: /How the language works/ })
      .click();
    await page.getByRole('button', { name: new RegExp(course.title) }).click();
    await expect(page.locator('.lesson-note')).toContainText(course.sample);
    await expect(
      page.getByRole('button', { name: /Unit check · Language foundations/ }),
    ).toBeVisible();
    if (course.name === 'Russian') {
      await page
        .getByRole('navigation', { name: 'Course units' })
        .getByRole('button', { name: /Cyrillic basics/ })
        .click();
      await page.getByRole('button', { name: /Read the Russian alphabet · Part 3/ }).click();
      await expect(page.locator('.lesson-note')).toContainText('Ъ ъ');
      await expect(page.locator('.lesson-note')).toContainText('Ь ь');
      await expect(page.locator('.lesson-note')).toContainText('Я я');
      await expect(
        page.getByRole('button', { name: /Unit check · Written Cyrillic/ }),
      ).toBeVisible();
    }
  });
}
