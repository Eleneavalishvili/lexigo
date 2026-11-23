import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { AuthStore } from '../accounts/auth-store';
import { billingGuard } from './billing-guard';

describe('Financial route access', () => {
  const user = signal<{ roles: string[]; permissions?: string[] } | null>(null);
  const restore = vi.fn();
  beforeEach(() => {
    restore.mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthStore, useValue: { user, restore } }],
    });
  });
  async function check() {
    return await TestBed.runInInjectionContext(() =>
      billingGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );
  }
  it('does not treat Admin alone or another staff role with a claim as financial access', async () => {
    user.set({ roles: ['Admin'] });
    expect(await check()).toBeInstanceOf(UrlTree);
    user.set({ roles: ['Teacher'], permissions: ['BillingManage'] });
    expect(await check()).toBeInstanceOf(UrlTree);
    user.set(null);
    expect(await check()).toBeInstanceOf(UrlTree);
  });
  it('allows a delegated Admin or Super Admin after account restoration', async () => {
    restore.mockImplementationOnce(async () =>
      user.set({ roles: ['Admin'], permissions: ['BillingManage'] }),
    );
    expect(await check()).toBe(true);
    user.set({ roles: ['SuperAdmin'] });
    expect(await check()).toBe(true);
  });
});
