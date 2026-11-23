import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStore } from '../../core/accounts/auth-store';
import { AdministrationApi } from '../../core/administration/administration-api';
import { ManagedUsers } from './users';
import { BillingAdminApi } from '../../core/billing/billing-admin-api';

describe('Account administration', () => {
  const user = signal<{ id: string; roles: string[] } | null>(null);
  const changed = vi.fn();
  const api = {
    users: vi.fn(),
    languages: vi.fn(),
    roles: vi.fn(),
    status: vi.fn(),
    history: vi.fn(),
  };
  const target = {
    id: 'target',
    email: 'team@example.test',
    displayName: 'Teaching Team',
    status: 'Active',
    roles: ['Learner'],
    languageIds: [],
    emailConfirmed: true,
    twoFactorEnabled: true,
  };
  const page = { total: 1, page: 1, pageSize: 20, items: [target] };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'actor', roles: ['Learner', 'SuperAdmin'] });
    api.users.mockResolvedValue(page);
    api.languages.mockResolvedValue([{ id: 2, englishName: 'Spanish' }]);
    api.roles.mockResolvedValue(undefined);
    api.status.mockResolvedValue(undefined);
    api.history.mockResolvedValue({ total: 0, page: 1, pageSize: 20, items: [] });
    TestBed.configureTestingModule({
      imports: [ManagedUsers],
      providers: [
        {
          provide: BillingAdminApi,
          useValue: {
            permission: vi.fn().mockResolvedValue({
              userId: 'target',
              granted: false,
              eligible: false,
              inherited: false,
            }),
          },
        },
        provideRouter([]),
        { provide: AuthStore, useValue: { user, credentialsChanged: changed } },
        { provide: AdministrationApi, useValue: api },
      ],
    });
  });
  const fill = (host: HTMLElement, selector: string, value: string) => {
    const input = host.querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  };
  const select = (host: HTMLElement) =>
    host.querySelector<HTMLButtonElement>('.user-list button')!.click();

  it('requires reason/password and sends selected roles/languages after explicit submission', async () => {
    const fixture = TestBed.createComponent(ManagedUsers);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    select(host);
    await fixture.whenStable();
    host
      .querySelector<HTMLFormElement>('.details form.account-proof')!
      .dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(api.roles).not.toHaveBeenCalled();
    const labels = Array.from(host.querySelectorAll<HTMLLabelElement>('.checks label'));
    labels
      .find((label) => label.textContent?.trim() === 'Reviewer')!
      .querySelector<HTMLInputElement>('input')!
      .click();
    labels
      .find((label) => label.textContent?.trim() === 'Spanish')!
      .querySelector<HTMLInputElement>('input')!
      .click();
    fill(host, '#change-reason', 'Translation review responsibility');
    fill(host, '#admin-password', 'Current password 42!');
    host
      .querySelector<HTMLFormElement>('.details form.account-proof')!
      .dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(api.roles).toHaveBeenCalledWith(
      'target',
      ['Learner', 'Reviewer'],
      [2],
      'Translation review responsibility',
      'Current password 42!',
    );
    expect(host.querySelector('[role="status"]')?.textContent).toContain('recorded');
    expect(host.querySelector('#admin-password')).toBeNull();
  });

  it('keeps roles and languages unavailable to ordinary Admin accounts', async () => {
    user.set({ id: 'actor', roles: ['Learner', 'Admin'] });
    const fixture = TestBed.createComponent(ManagedUsers);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    select(host);
    await fixture.whenStable();
    expect(host.querySelector('fieldset')).toBeNull();
    expect(api.languages).not.toHaveBeenCalled();
    fill(host, '#change-reason', 'Moderation access review');
    fill(host, '#admin-password', 'Current password 42!');
    host
      .querySelector<HTMLFormElement>('.details form.account-proof')!
      .dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(api.status).not.toHaveBeenCalled();
    let dialog = document.querySelector<HTMLElement>('[role="dialog"]')!;
    expect(dialog.getAttribute('aria-labelledby')).toBe('block-account-title');
    expect(dialog.textContent).toContain('Teaching Team');
    expect(document.activeElement).toBe(dialog.querySelector('.button-outline'));
    dialog.querySelector<HTMLButtonElement>('.button-outline')!.click();
    await fixture.whenStable();
    expect(api.status).not.toHaveBeenCalled();
    host
      .querySelector<HTMLFormElement>('.details form.account-proof')!
      .dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    dialog = document.querySelector<HTMLElement>('[role="dialog"]')!;
    dialog.querySelector<HTMLButtonElement>('.button-red')!.click();
    await fixture.whenStable();
    expect(api.status).toHaveBeenCalledWith(
      'target',
      'Blocked',
      'Moderation access review',
      'Current password 42!',
    );
    expect(api.roles).not.toHaveBeenCalled();
  });

  it('shows the server last-administrator rejection without claiming success', async () => {
    api.roles.mockRejectedValue(
      new HttpErrorResponse({
        status: 409,
        error: { title: 'The last active Super Admin must keep access.' },
      }),
    );
    const fixture = TestBed.createComponent(ManagedUsers);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    select(host);
    await fixture.whenStable();
    fill(host, '#change-reason', 'Administrative access review');
    fill(host, '#admin-password', 'Current password 42!');
    host
      .querySelector<HTMLFormElement>('.details form.account-proof')!
      .dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('last active Super Admin');
    expect(host.querySelector('.notice')).toBeNull();
  });

  it('clears account details and proof on an account switch', async () => {
    const fixture = TestBed.createComponent(ManagedUsers);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    select(host);
    await fixture.whenStable();
    fill(host, '#admin-password', 'Private test password 42!');
    user.set({ id: 'other', roles: ['Admin'] });
    await fixture.whenStable();
    expect(host.querySelector('#admin-password')).toBeNull();
    expect(host.querySelector('fieldset')).toBeNull();
  });
});
