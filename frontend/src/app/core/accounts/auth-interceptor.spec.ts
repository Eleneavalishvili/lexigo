import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from './auth-store';
import { authInterceptor } from './auth-interceptor';

describe('Account-bound API requests', () => {
  const user = signal<{ id: string } | null>({ id: 'first' });
  const auth = { user, token: vi.fn(), refresh: vi.fn() };
  let http: HttpClient;
  let requests: HttpTestingController;
  const url = '/api/v1/me/leaderboard-participation';
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'first' });
    auth.token.mockResolvedValue('first-token');
    auth.refresh.mockResolvedValue({ accessToken: 'refreshed-first-token', user: { id: 'first' } });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthStore, useValue: auth },
      ],
    });
    http = TestBed.inject(HttpClient);
    requests = TestBed.inject(HttpTestingController);
  });
  afterEach(() => requests.verify());
  it('does not dispatch an old action after the account changes while a token is pending', async () => {
    let resolve!: (token: string) => void;
    auth.token.mockReturnValue(new Promise<string>((done) => (resolve = done)));
    const pending = firstValueFrom(http.put(url, { optedIn: true })).catch(
      (error) => error as HttpErrorResponse,
    );
    user.set({ id: 'second' });
    resolve('second-token');
    const error = (await pending) as HttpErrorResponse;
    expect(error.status).toBe(409);
    requests.expectNone(url);
    expect(auth.refresh).not.toHaveBeenCalled();
  });
  it('does not refresh or retry an old request under a different account', async () => {
    const pending = firstValueFrom(http.put(url, { optedIn: true })).catch(
      (error) => error as HttpErrorResponse,
    );
    await Promise.resolve();
    const first = requests.expectOne(url);
    expect(first.request.headers.get('Authorization')).toBe('Bearer first-token');
    user.set({ id: 'second' });
    first.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(((await pending) as HttpErrorResponse).status).toBe(409);
    expect(auth.refresh).not.toHaveBeenCalled();
    requests.expectNone(url);
  });
  it('does not replay when the account changes during a pending refresh', async () => {
    let resolve!: (value: unknown) => void;
    auth.refresh.mockReturnValue(new Promise((done) => (resolve = done)));
    const pending = firstValueFrom(http.put(url, { optedIn: true })).catch(
      (error) => error as HttpErrorResponse,
    );
    await Promise.resolve();
    requests.expectOne(url).flush({}, { status: 401, statusText: 'Unauthorized' });
    user.set({ id: 'second' });
    resolve({ accessToken: 'second-token', user: { id: 'second' } });
    expect(((await pending) as HttpErrorResponse).status).toBe(409);
    requests.expectNone(url);
  });
  it('still refreshes for the same owner and rejects a late private response after sign-out', async () => {
    const first = firstValueFrom(http.put(url, { optedIn: true }));
    await Promise.resolve();
    requests.expectOne(url).flush({}, { status: 401, statusText: 'Unauthorized' });
    await Promise.resolve();
    const retry = requests.expectOne(url);
    expect(retry.request.headers.get('Authorization')).toBe('Bearer refreshed-first-token');
    retry.flush(null);
    await first;
    const late = firstValueFrom(http.get('/api/v1/me/dashboard')).catch(
      (error) => error as HttpErrorResponse,
    );
    await Promise.resolve();
    const read = requests.expectOne('/api/v1/me/dashboard');
    user.set(null);
    read.flush({ totalXp: 100 });
    expect(((await late) as HttpErrorResponse).status).toBe(409);
  });
});
