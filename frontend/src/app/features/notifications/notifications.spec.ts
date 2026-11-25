import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NotificationsStore } from '../../core/accounts/notifications-store';
import { Notifications } from './notifications';

describe('Account inbox rendering', () => {
  it('opens teacher conversations from study notifications without marking them read', async () => {
    const store = {
      items: signal(
        ['ExtraStudyMessage', 'ExtraStudyRequested', 'ExtraStudySchedule'].map((type) => ({
          id: type,
          type,
          title: 'Study update',
          body: 'A private update is available.',
          createdAt: '2026-11-25T00:00:00Z',
          readAt: null,
        })),
      ),
      loading: signal(false),
      reading: signal(null),
      error: signal(''),
      more: signal(false),
      load: vi.fn(),
      read: vi.fn(),
    };
    TestBed.configureTestingModule({ imports: [Notifications], providers: [provideRouter([])] });
    TestBed.overrideComponent(Notifications, {
      set: { providers: [{ provide: NotificationsStore, useValue: store }] },
    });
    const fixture = TestBed.createComponent(Notifications);
    await fixture.whenStable();
    const links = fixture.nativeElement.querySelectorAll('li a[href="/classes/extra-study"]');
    expect(links).toHaveLength(3);
    expect(fixture.nativeElement.querySelector('a[href="/classes"]')).not.toBeNull();
    expect(store.read).not.toHaveBeenCalled();
  });
  it('escapes notification content and only marks a postcard read after an explicit click', async () => {
    const store = {
      items: signal([
        {
          id: 'one',
          title: '<img src=x onerror=alert(1)>',
          body: '<script>private</script>',
          createdAt: '2026-10-29T00:00:00Z',
          readAt: null,
        },
      ]),
      loading: signal(false),
      reading: signal(null),
      error: signal(''),
      more: signal(false),
      load: vi.fn(),
      read: vi.fn(),
    };
    TestBed.configureTestingModule({ imports: [Notifications], providers: [provideRouter([])] });
    TestBed.overrideComponent(Notifications, {
      set: { providers: [{ provide: NotificationsStore, useValue: store }] },
    });
    const fixture = TestBed.createComponent(Notifications);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('<script>private</script>');
    expect(host.querySelector('script')).toBeNull();
    expect(host.querySelector('img')).toBeNull();
    expect(store.read).not.toHaveBeenCalled();
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((value) => value.textContent?.includes('Mark as read'))!
      .click();
    expect(store.read).toHaveBeenCalledExactlyOnceWith('one');
  });
});
