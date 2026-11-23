import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../../core/accounts/auth-store';
import { BillingAdminApi } from '../../core/billing/billing-admin-api';
import { PlanPriceSchedule } from './plan-price-schedule';

it('retries an uncertain schedule with the same request key and a freshly entered password', async () => {
  const schedulePrice = vi
    .fn()
    .mockRejectedValueOnce(new Error('Disconnected'))
    .mockResolvedValue({});
  TestBed.configureTestingModule({
    imports: [PlanPriceSchedule],
    providers: [
      { provide: AuthStore, useValue: { user: () => ({ id: 'staff' }) } },
      { provide: BillingAdminApi, useValue: { schedulePrice } },
    ],
  });
  const fixture = TestBed.createComponent(PlanPriceSchedule);
  fixture.componentRef.setInput('priceId', 'previous-price');
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
  const submit = () => host.querySelector('form')!.dispatchEvent(new Event('submit'));
  fill('amount', '12.50');
  fill('startsAt', '2027-02-01T15:00');
  fill('reason', 'Scheduled test price');
  const password = fill('currentPassword', 'first entry');
  submit();
  await fixture.whenStable();
  expect(password.value).toBe('');
  expect(host.querySelector('fieldset')?.disabled).toBe(true);
  fill('currentPassword', 'second entry');
  submit();
  await fixture.whenStable();
  const [first, second] = schedulePrice.mock.calls.map((call) => call[1]);
  expect(second).toEqual({ ...first, currentPassword: 'second entry' });
  expect(saved).toHaveBeenCalledOnce();
});
