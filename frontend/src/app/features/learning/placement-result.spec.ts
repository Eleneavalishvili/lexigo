import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { LearningApi, PlacementResult } from '../../core/learning/learning-api';
import { PlacementResultPage } from './placement-result';

@Component({ template: '' })
class Destination {}

describe('Placement recommendation', () => {
  const user = signal<{ id: string } | null>({ id: 'learner' });
  const api = { placementResult: vi.fn(), applyPlacement: vi.fn() };
  const result: PlacementResult = {
    sessionId: 'check',
    courseId: 'course',
    completed: true,
    applied: false,
    recommendedUnitId: 'unit',
    recommendedTitle: 'Everyday essentials',
    units: [{ id: 'unit', title: 'Everyday essentials', correct: 3, total: 5 }],
  };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'learner' });
    api.placementResult.mockResolvedValue(result);
    api.applyPlacement.mockResolvedValue({ ...result, applied: true });
    TestBed.configureTestingModule({
      imports: [PlacementResultPage],
      providers: [
        provideRouter([{ path: 'login', component: Destination }]),
        { provide: AuthStore, useValue: { user } },
        { provide: LearningApi, useValue: api },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ sessionId: 'check' })) },
        },
      ],
    });
  });
  it('shows server results and applies only after an explicit action without sending a unit or score', async () => {
    const fixture = TestBed.createComponent(PlacementResultPage);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Everyday essentials');
    expect(host.textContent).toContain('3 / 5');
    expect(host.textContent).toContain('earn no XP');
    expect(api.applyPlacement).not.toHaveBeenCalled();
    host.querySelector<HTMLButtonElement>('.button-red')!.click();
    await fixture.whenStable();
    expect(api.applyPlacement).toHaveBeenCalledWith('check');
    expect(host.textContent).toContain('Your starting point is saved');
    expect(host.querySelector('button.button-red')).toBeNull();
  });
  it('keeps incomplete checks resumable without offering to apply them', async () => {
    api.placementResult.mockResolvedValue({
      ...result,
      completed: false,
      units: [],
      recommendedUnitId: null,
    });
    const fixture = TestBed.createComponent(PlacementResultPage);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector<HTMLAnchorElement>('.button-red')!.getAttribute('href')).toBe(
      '/learn/session/check',
    );
    expect(host.querySelector('button.button-red')).toBeNull();
    expect(api.applyPlacement).not.toHaveBeenCalled();
  });
  it('discards another account’s late result', async () => {
    let resolve!: (value: PlacementResult) => void;
    api.placementResult.mockReturnValue(
      new Promise<PlacementResult>((done) => {
        resolve = done;
      }),
    );
    const fixture = TestBed.createComponent(PlacementResultPage);
    fixture.detectChanges();
    user.set({ id: 'different-learner' });
    fixture.detectChanges();
    resolve(result);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Everyday essentials');
    expect(api.applyPlacement).not.toHaveBeenCalled();
  });
});
