import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { AdministrationApi } from '../../core/administration/administration-api';
import { AuditLog } from './audit-log';

describe('Administrative audit log', () => {
  it('loads the global pages and hides the previous account’s history when the account changes', async () => {
    const user = signal({ id: 'super-admin', roles: ['SuperAdmin'] });
    const history = vi.fn(async (_userId: string | null, page: number) => ({
      total: 21,
      page,
      pageSize: 20,
      items: [
        {
          id: String(page),
          actorId: 'actor',
          entityType: 'Lesson',
          entityId: 'lesson-id',
          action: 'LessonPublished',
          reason: 'Reviewed lesson',
          traceId: 'request-trace',
          occurredAt: '2026-11-04T08:00:00Z',
        },
      ],
    }));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
        { provide: AdministrationApi, useValue: { history } },
      ],
    });
    const fixture = TestBed.createComponent(AuditLog);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(history).toHaveBeenCalledWith(null, 1);
    expect(host.textContent).toContain('LessonPublished');

    const next = [...host.querySelectorAll('button')].find(
      (button) => button.textContent?.trim() === 'Next',
    )!;
    next.click();
    await fixture.whenStable();
    expect(history).toHaveBeenLastCalledWith(null, 2);
    expect(host.textContent).toContain('Page 2');

    user.set({ id: 'another-admin', roles: ['SuperAdmin'] });
    await fixture.whenStable();
    expect(host.textContent).not.toContain('LessonPublished');
  });
});
