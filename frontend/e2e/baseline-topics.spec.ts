import { expect, test } from '@playwright/test';

for (const course of [
  { name: 'Spanish', code: 'es', day: 'lunes', date: 'Hoy es el dos de mayo.' },
  { name: 'Italian', code: 'it', day: 'lunedì', date: 'Oggi è il due maggio.' },
  { name: 'German', code: 'de', day: 'Montag', date: 'Heute ist der zweite Mai.' },
  { name: 'Russian', code: 'ru', day: 'понедельник', date: 'Сегодня второе мая.' },
]) {
  test(`${course.name} teaches calendar dates and school vocabulary with playable reference audio`, async ({
    page,
    request,
  }) => {
    await page.goto('/courses');
    await page.getByRole('button', { name: new RegExp(course.name) }).click();
    const units = page.getByRole('navigation', { name: 'Course units' });
    await units.getByRole('button', { name: /Dates, work and study/ }).click();
    await page.getByRole('button', { name: /Days and calendar dates/ }).click();
    const notes = page.locator('.lesson-note');
    await expect(notes).toContainText(course.day);
    await expect(notes).toContainText(course.date);
    await page.getByRole('button', { name: /People at school and work/ }).click();
    await expect(notes).toContainText('male teacher');
    await expect(notes).toContainText('I study English.');
    await expect(
      page.getByRole('button', { name: /Unit check · Dates, work and study/ }),
    ).toBeVisible();

    const manifestResponse = await request.get('/pronunciation/manifest.json');
    expect(manifestResponse.ok()).toBeTruthy();
    const manifest = (await manifestResponse.json()) as {
      clips: Record<string, { file: string; language: string }>;
    };
    const key = `${course.code}|${course.date.toLowerCase().replace(/\.$/, '')}`;
    const clip = manifest.clips[key];
    expect(clip.language).toBe(course.code);
    expect(clip.file).toMatch(/^[a-f0-9]{24}\.wav$/);
    const response = await request.get(`/pronunciation/${clip.file}`);
    expect(response.ok()).toBeTruthy();
    const wave = await response.body();
    expect(wave.length).toBeGreaterThan(44);
    expect(wave.subarray(0, 4).toString('ascii')).toBe('RIFF');
    expect(wave.subarray(8, 12).toString('ascii')).toBe('WAVE');
    await page.evaluate(async (file) => {
      const audio = new Audio(`/pronunciation/${file}`);
      await audio.play();
      audio.pause();
    }, clip.file);
  });
}
