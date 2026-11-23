import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { SystemSettingsPage } from './system-settings';

describe('System reward settings', () => {
  const user = signal({ id: 'administrator', roles: ['SuperAdmin'] });
  const defaults = {
    lessonXp: 20,
    dialogueXp: 10,
    dailyGoalXp: 20,
    audioUploadMiB: 10,
    studyDays: 31,
    reminderTime: '18:00:00',
  };
  const endpoint = '/api/v1/admin/settings';

  beforeEach(() => {
    user.set({ id: 'administrator', roles: ['SuperAdmin'] });
    TestBed.configureTestingModule({
      imports: [SystemSettingsPage],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthStore, useValue: { user } },
      ],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('saves the independent conversation reward with a reason and revision then reads the server result', async () => {
    const fixture = TestBed.createComponent(SystemSettingsPage);
    const http = TestBed.inject(HttpTestingController);
    const stamp = '2026-11-20T10:00:00Z';
    http.expectOne(endpoint).flush({ defaults, updatedAt: stamp });
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const amount = host.querySelector<HTMLInputElement>('[formControlName="dialogueXp"]')!;
    amount.value = '17';
    amount.dispatchEvent(new Event('input'));
    const reason = host.querySelector<HTMLTextAreaElement>('textarea')!;
    reason.value = 'Adjust the conversation reward';
    reason.dispatchEvent(new Event('input'));
    host.querySelector('form')!.dispatchEvent(new Event('submit'));
    const update = http.expectOne(
      (request) => request.url === endpoint && request.method === 'PUT',
    );
    expect(update.request.body).toEqual({
      defaults: { ...defaults, dialogueXp: 17 },
      expectedUpdatedAt: stamp,
      reason: reason.value,
    });
    update.flush(null);
    await Promise.resolve();
    http.expectOne(endpoint).flush({ defaults: { ...defaults, dialogueXp: 17 }, updatedAt: stamp });
    await fixture.whenStable();
    expect(amount.value).toBe('17');
    expect(host.textContent).toContain('Settings saved.');
    expect(host.textContent).toContain('free review practice earn 0 XP');
  });

  it('does not load privileged settings for a teacher', async () => {
    user.set({ id: 'teacher', roles: ['Teacher'] });
    const fixture = TestBed.createComponent(SystemSettingsPage);
    await fixture.whenStable();
    TestBed.inject(HttpTestingController).expectNone(endpoint);
    expect(fixture.nativeElement.textContent).not.toContain('System settings');
  });

  it('does not submit a saved form after switching to another administrator account', async () => {
    const fixture = TestBed.createComponent(SystemSettingsPage);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(endpoint).flush({ defaults, updatedAt: null });
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const reason = host.querySelector<HTMLTextAreaElement>('textarea')!;
    reason.value = 'An old account form';
    reason.dispatchEvent(new Event('input'));
    const previousForm = host.querySelector('form')!;
    user.set({ id: 'different-administrator', roles: ['SuperAdmin'] });
    previousForm.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    http.expectNone(endpoint);
    expect(host.textContent).not.toContain('System settings');
  });
});
