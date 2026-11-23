import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStore } from '../../core/accounts/auth-store';
import { BillingAdminApi } from '../../core/billing/billing-admin-api';
import { BillingPermissionEditor } from './billing-permission-editor';

describe('Explicit billing delegation', () => {
  const user = signal({ id: 'actor', roles: ['SuperAdmin'] });
  const api = { permission: vi.fn(), setPermission: vi.fn() };
  const view = { userId: 'target', granted: false, eligible: true, inherited: false };
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'actor', roles: ['SuperAdmin'] });
    api.permission.mockResolvedValue(view);
    api.setPermission.mockResolvedValue({ ...view, granted: true });
    TestBed.configureTestingModule({
      imports: [BillingPermissionEditor],
      providers: [
        { provide: AuthStore, useValue: { user } },
        { provide: BillingAdminApi, useValue: api },
      ],
    });
  });
  async function open() {
    const fixture = TestBed.createComponent(BillingPermissionEditor);
    fixture.componentRef.setInput('userId', 'target');
    await fixture.whenStable();
    return fixture;
  }
  function fill(host: HTMLElement) {
    const enabled = host.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
    enabled.checked = true;
    enabled.dispatchEvent(new Event('change'));
    const reason = host.querySelector<HTMLTextAreaElement>('textarea')!;
    reason.value = 'Reviewed finance duties';
    reason.dispatchEvent(new Event('input'));
    const password = host.querySelector<HTMLInputElement>('#billing-permission-password')!;
    password.value = 'Transient passphrase 42!';
    password.dispatchEvent(new Event('input'));
  }
  it('never grants on render and clears the proof after an explicit save', async () => {
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    expect(api.setPermission).not.toHaveBeenCalled();
    fill(host);
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(api.setPermission).toHaveBeenCalledExactlyOnceWith('target', {
      enabled: true,
      reason: 'Reviewed finance duties',
      currentPassword: 'Transient passphrase 42!',
      idempotencyKey: expect.any(String),
    });
    expect(host.querySelector<HTMLInputElement>('#billing-permission-password')!.value).toBe('');
  });
  it('retries uncertain grants with the same immutable key and intent', async () => {
    api.setPermission.mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }));
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    fill(host);
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    const first = api.setPermission.mock.calls[0][1];
    const password = host.querySelector<HTMLInputElement>('#billing-permission-password')!;
    password.value = 'Transient passphrase 42!';
    password.dispatchEvent(new Event('input'));
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(api.setPermission.mock.calls[1][1]).toEqual(first);
  });
});
