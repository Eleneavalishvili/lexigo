import { AxeBuilder } from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

const courses = [
  { code: 'es', name: 'Spanish', example: 'el agua fría' },
  { code: 'it', name: 'Italian', example: 'un uomo / un’amica' },
  { code: 'de', name: 'German', example: 'Ich sehe den Mann.' },
  { code: 'ru', name: 'Russian', example: 'У меня нет книги.' },
];

test('each catalog course opens its own accessible language-specific reference', async ({
  page,
}, info) => {
  for (const course of courses) {
    await page.goto('/courses');
    await page.getByRole('button', { name: new RegExp(course.name) }).click();
    await page
      .getByRole('link', { name: new RegExp(`Read the .*${course.name}.* basics`) })
      .click();
    await expect(page).toHaveURL(new RegExp(`/basics/${course.code}$`));
    await expect(page.getByRole('heading', { level: 1 })).toContainText(course.name);
    await expect(page.getByText(course.example, { exact: true })).toBeVisible();
    await expect(page.locator('dt').first()).toHaveAttribute('lang', course.code);
    await expect(page.getByText('Reference study uses no hearts', { exact: false })).toBeVisible();
    for (const width of info.project.name === 'phone' ? [320, 390] : [768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      const results = await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
        .analyze();
      expect(results.violations, `${course.code} at ${width}px`).toEqual([]);
      expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBe(
        false,
      );
    }
    const topics = page.getByRole('navigation', { name: 'Basics topics' });
    await topics.getByRole('link').last().click();
    await expect(page).toHaveURL(/#basics-\d$/);
    await page.reload();
    await expect(page.getByRole('heading', { level: 1 })).toContainText(course.name);
  }
  await page.getByRole('link', { name: /Explore all 33 Russian letters/ }).click();
  await expect(page).toHaveURL(/\/alphabet\/russian$/);
  await expect(
    page.getByRole('group', { name: 'Russian letters' }).getByRole('button'),
  ).toHaveCount(33);
});

test('unsupported and inherited-object language names offer a safe course link', async ({
  page,
}) => {
  for (const code of ['fr', 'constructor', '__proto__']) {
    await page.goto(`/basics/${code}`);
    await expect(page.getByRole('heading', { name: 'Choose a language.' })).toBeVisible();
    await expect(page.locator('section')).toHaveCount(0);
    await page.getByRole('link', { name: 'Find your course' }).click();
    await expect(page).toHaveURL(/\/courses$/);
  }
});
