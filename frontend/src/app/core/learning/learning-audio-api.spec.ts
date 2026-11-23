import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LearningApi } from './learning-api';

describe('Learning audio transport', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('sends only the recording and owned question/request identifiers, and cancels on exit', async () => {
    const controller = new AbortController();
    const pending = TestBed.inject(LearningApi).checkSpeech(
      'session',
      'exercise',
      'request-key',
      new Blob(['voice'], { type: 'audio/wav' }),
      controller.signal,
    );
    const rejected = expect(pending).rejects.toThrow();
    const request = TestBed.inject(HttpTestingController).expectOne(
      '/api/v1/me/learning-sessions/session/speech-checks',
    );
    expect(request.request.method).toBe('POST');
    const form = request.request.body as FormData;
    expect(Array.from(form.keys()).sort()).toEqual(['ExerciseId', 'IdempotencyKey', 'Recording']);
    expect(form.get('ExerciseId')).toBe('exercise');
    expect(form.get('IdempotencyKey')).toBe('request-key');
    controller.abort();
    await rejected;
    expect(request.cancelled).toBe(true);
  });
  it('requests an option through its owned session/exercise path without accepting a storage key', async () => {
    const pending = TestBed.inject(LearningApi).audio('session', 'exercise', 'option/one');
    const request = TestBed.inject(HttpTestingController).expectOne(
      '/api/v1/me/learning-sessions/session/exercises/exercise/options/option%2Fone/audio',
    );
    expect(request.request.method).toBe('GET');
    request.flush({ url: 'http://localhost:3900/private/signed', expiresInSeconds: 300 });
    expect((await pending).expiresInSeconds).toBe(300);
  });
});
