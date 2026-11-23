import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../../core/accounts/auth-store';
import { BillingAdminApi } from '../../core/billing/billing-admin-api';
import { PlanMetadataEditor } from './plan-metadata-editor';

it('submits the displayed plan name as the expected version and clears the password', async () => {
  const renamePlan = vi.fn().mockResolvedValue(undefined);
  TestBed.configureTestingModule({
    imports: [PlanMetadataEditor],
    providers: [
      { provide: AuthStore, useValue: { user: () => ({ id: 'staff' }) } },
      { provide: BillingAdminApi, useValue: { renamePlan } },
    ],
  });
  const fixture = TestBed.createComponent(PlanMetadataEditor);
  fixture.componentRef.setInput('plan', {
    id: 'plan',
    name: 'Existing plan',
    isActive: true,
    prices: [],
  });
  const saved = vi.fn();
  fixture.componentInstance.saved.subscribe(saved);
  await fixture.whenStable();
  const host: HTMLElement = fixture.nativeElement;
  const fill = (name: string, value: string) => {
    const field = host.querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!;
    field.value = value;
    field.dispatchEvent(new Event('input'));
    return field;
  };
  fill('name', 'Renamed plan');
  fill('reason', 'Catalogue correction');
  const password = fill('currentPassword', 'Current test password');
  host.querySelector('form')!.dispatchEvent(new Event('submit'));
  await fixture.whenStable();
  expect(renamePlan).toHaveBeenCalledWith('plan', {
    name: 'Renamed plan',
    expectedName: 'Existing plan',
    reason: 'Catalogue correction',
    currentPassword: 'Current test password',
    idempotencyKey: expect.any(String),
  });
  expect(password.value).toBe('');
  expect(saved).toHaveBeenCalledOnce();
});
