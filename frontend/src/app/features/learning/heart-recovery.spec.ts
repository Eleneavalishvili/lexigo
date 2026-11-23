import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../../core/accounts/auth-store';
import { HeartsApi, HeartStatus } from '../../core/learning/hearts-api';
import { HeartRecovery } from './heart-recovery';

describe('Server-timed heart recovery', () => {
  const user = signal<{ id: string } | null>({ id: 'owner' });
  const api = { status: vi.fn() };
  const exhausted: HeartStatus = {
    count: 0,
    maximum: 5,
    serverNow: '2026-10-28T19:00:00Z',
    refillAt: '2026-10-28T20:00:00Z',
  };
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'owner' });
    api.status.mockResolvedValue(exhausted);
    TestBed.configureTestingModule({
      imports: [HeartRecovery],
      providers: [
        { provide: AuthStore, useValue: { user } },
        { provide: HeartsApi, useValue: api },
      ],
    });
  });
  afterEach(() => vi.useRealTimers());
  it('shows a full-hour countdown based on server timestamps', async () => {
    const fixture = TestBed.createComponent(HeartRecovery);
    fixture.componentRef.setInput('count', 0);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('01:00:00');
    expect(fixture.nativeElement.querySelector('[role=timer]').getAttribute('aria-live')).toBe(
      'off',
    );
    vi.spyOn(Date, 'now').mockReturnValue(0);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('01:00:00');
    vi.restoreAllMocks();
  });
  it('rechecks with the server before announcing recovery', async () => {
    const fixture = TestBed.createComponent(HeartRecovery);
    fixture.componentRef.setInput('count', 0);
    const recovered = vi.fn();
    fixture.componentInstance.recovered.subscribe(recovered);
    await fixture.whenStable();
    api.status.mockResolvedValue({ ...exhausted, count: 5, refillAt: null });
    fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(recovered).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.querySelector('[role=timer]')).toBeNull();
  });
  it('clears the timer and ignores late responses when the account changes', async () => {
    let finish!: (value: HeartStatus) => void;
    api.status.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    const fixture = TestBed.createComponent(HeartRecovery);
    fixture.detectChanges();
    user.set({ id: 'someone-else' });
    fixture.detectChanges();
    finish(exhausted);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=timer]')).toBeNull();
  });
  it('shows an error without manufacturing a refill', async () => {
    api.status.mockRejectedValueOnce(new Error('offline'));
    const fixture = TestBed.createComponent(HeartRecovery);
    const recovered = vi.fn();
    fixture.componentInstance.recovered.subscribe(recovered);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('couldn’t check your hearts');
    expect(recovered).not.toHaveBeenCalled();
  });
  it('waits for the server at the countdown boundary instead of granting hearts locally', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'performance'] });
    api.status.mockResolvedValueOnce({ ...exhausted, refillAt: '2026-10-28T19:00:01Z' });
    const fixture = TestBed.createComponent(HeartRecovery);
    fixture.componentRef.setInput('count', 0);
    const recovered = vi.fn();
    fixture.componentInstance.recovered.subscribe(recovered);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('00:00:01');
    expect(recovered).not.toHaveBeenCalled();
    api.status.mockResolvedValue({ ...exhausted, count: 5, refillAt: null });
    await vi.advanceTimersByTimeAsync(1000);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(api.status).toHaveBeenCalledTimes(2);
    expect(recovered).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.querySelector('[role=timer]')).toBeNull();
  });
});
