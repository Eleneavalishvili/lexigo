import { TestBed } from '@angular/core/testing';
import { ChallengeEditor } from './challenge-editor';
import { AchievementEditor } from './achievement-editor';

function fill(host: HTMLElement, selector: string, value: string): void {
  const input = host.querySelector<HTMLInputElement>(selector)!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
  input.dispatchEvent(new Event('change'));
}

describe('Reward editors', () => {
  it('serializes explicitly labelled UTC times without the browser local-time offset', async () => {
    TestBed.configureTestingModule({ imports: [ChallengeEditor] });
    const fixture = TestBed.createComponent(ChallengeEditor);
    const saved = vi.fn();
    fixture.componentInstance.save.subscribe(saved);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    fill(host, '#challenge-title', 'A thoughtful week');
    fill(host, '#challenge-start', '2026-11-10T09:30');
    fill(host, '#challenge-end', '2026-11-17T09:30');
    fill(host, '#challenge-reason', 'Schedule a new weekly learning goal.');
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(saved).toHaveBeenCalledWith(
      expect.objectContaining({
        startsAt: '2026-11-10T09:30:00.000Z',
        endsAt: '2026-11-17T09:30:00.000Z',
      }),
    );
  });
  it('prevents invalid date order from emitting a mutation', async () => {
    TestBed.configureTestingModule({ imports: [ChallengeEditor] });
    const fixture = TestBed.createComponent(ChallengeEditor);
    const saved = vi.fn();
    fixture.componentInstance.save.subscribe(saved);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    fill(host, '#challenge-title', 'A thoughtful week');
    fill(host, '#challenge-start', '2026-11-17T09:30');
    fill(host, '#challenge-end', '2026-11-10T09:30');
    fill(host, '#challenge-reason', 'Schedule a new weekly learning goal.');
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(saved).not.toHaveBeenCalled();
    expect(host.textContent).toContain('end time after the start');
  });
  it('keeps permanent badge codes readonly and requires an audit reason', async () => {
    TestBed.configureTestingModule({ imports: [AchievementEditor] });
    const fixture = TestBed.createComponent(AchievementEditor);
    fixture.componentRef.setInput('value', {
      id: 'badge',
      code: 'fixed-code',
      title: '<img src=x>',
      ruleType: 'CompletedLessons',
      threshold: 1,
      revision: 2,
      canEdit: true,
      isArchived: false,
    });
    const saved = vi.fn();
    fixture.componentInstance.save.subscribe(saved);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector<HTMLInputElement>('#badge-code')!.readOnly).toBe(true);
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    expect(saved).not.toHaveBeenCalled();
    expect(host.querySelector('img')).toBeNull();
  });
  it('preserves precise stored timestamps when only other challenge fields change', async () => {
    TestBed.configureTestingModule({ imports: [ChallengeEditor] });
    const fixture = TestBed.createComponent(ChallengeEditor);
    fixture.componentRef.setInput('value', {
      id: 'future',
      title: 'A future week',
      ruleType: 'CompletedLessons',
      target: 3,
      rewardXp: 20,
      startsAt: '2026-11-10T09:30:42.123456Z',
      endsAt: '2026-11-17T09:30:42.123456Z',
      revision: 1,
      isArchived: false,
      canEdit: true,
    });
    const saved = vi.fn();
    fixture.componentInstance.save.subscribe(saved);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    fill(host, '#challenge-reason', 'Adjust the future reward policy.');
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(saved).toHaveBeenCalledWith(
      expect.objectContaining({
        startsAt: '2026-11-10T09:30:42.123456Z',
        endsAt: '2026-11-17T09:30:42.123456Z',
      }),
    );
  });
});
