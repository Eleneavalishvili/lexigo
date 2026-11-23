import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Meta } from '@angular/platform-browser';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CompletionCardsApi } from '../../core/learning/completion-cards-api';
import { SharedCompletionCardPage } from './shared-completion-card';

describe('Shared completion card', () => {
  const api = { shared: vi.fn() };
  beforeEach(() => {
    vi.resetAllMocks();
    api.shared.mockResolvedValue({
      displayName: 'Fixture learner',
      courseTitle: 'Spanish',
      unitTitle: 'First words',
      verificationCode: 'a'.repeat(32),
      lessonCount: 2,
      gradedLessonCount: 0,
      averageBestScore: null,
      completedAt: '2026-11-06T10:00:00Z',
      issuedAt: '2026-11-06T10:00:01Z',
    });
    TestBed.configureTestingModule({
      imports: [SharedCompletionCardPage],
      providers: [
        provideRouter([]),
        { provide: CompletionCardsApi, useValue: api },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ code: 'shared-code' })) },
        },
      ],
    });
  });
  it('renders ungraded work without a score and removes robots metadata after leaving', async () => {
    const fixture = TestBed.createComponent(SharedCompletionCardPage);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Practice completed · ungraded',
    );
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('100%');
    const report = (fixture.nativeElement as HTMLElement).querySelector('a[href^="/reports"]');
    expect(report?.textContent?.trim()).toBe('Report the public name');
    expect(report?.getAttribute('href')).toBe('/reports?card=shared-code');
    expect(TestBed.inject(Meta).getTag('name="robots"')?.content).toContain('noindex');
    fixture.destroy();
    expect(TestBed.inject(Meta).getTag('name="robots"')).toBeNull();
  });
  it('does not distinguish private and revoked links or display a cached card after denial', async () => {
    api.shared.mockRejectedValueOnce(new HttpErrorResponse({ status: 404 }));
    const fixture = TestBed.createComponent(SharedCompletionCardPage);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('private, no longer shared');
    expect(host.querySelector('app-completion-card-art')).toBeNull();
    expect(host.querySelector('a[href^="/reports"]')).toBeNull();
  });
});
