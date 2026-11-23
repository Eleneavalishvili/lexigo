import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from './auth-store';
import { AuthResponse } from './auth-models';

describe('AuthStore', () => {
  let store: AuthStore;
  let http: HttpTestingController;
  const session: AuthResponse = {
    accessToken: 'memory-only-access',
    expiresAt: new Date(Date.now() + 300_000).toISOString(),
    user: {
      id: 'learner-id',
      email: 'learner@example.test',
      displayName: 'Learner',
      emailConfirmed: true,
      roles: ['Learner'],
    },
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    store = TestBed.inject(AuthStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function csrf(): Promise<void> {
    // A browser Web Lock can defer the request until the next task.
    await new Promise((resolve) => setTimeout(resolve, 0));
    http.expectOne('/api/v1/auth/csrf').flush({ requestToken: 'csrf-request-token' });
    await Promise.resolve();
  }

  it('shares a refresh request and keeps the access token in memory', async () => {
    const first = store.refresh();
    const second = store.refresh();
    expect(first).toBe(second);
    await csrf();
    const request = http.expectOne('/api/v1/auth/refresh');
    expect(request.request.headers.get('X-XSRF-TOKEN')).toBe('csrf-request-token');
    request.flush(session);
    await first;
    expect(await store.token()).toBe(session.accessToken);
    expect(store.user()?.email).toBe(session.user.email);
    expect(localStorage.getItem('accessToken')).toBeNull();
  });

  it('treats an expired or revoked refresh as signed out', async () => {
    const pending = store.restore();
    await csrf();
    http.expectOne('/api/v1/auth/refresh').flush({}, { status: 401, statusText: 'Unauthorized' });
    await pending;
    expect(store.user()).toBeNull();
    expect(await store.token()).toBeNull();
  });

  it('does not create an account session before the second factor is accepted', async () => {
    const pending = store.login('learner@example.test', 'Long learner password 42!');
    await csrf();
    http.expectOne('/api/v1/auth/login').flush({ requiresTwoFactor: true });
    expect(await pending).toEqual({ requiresTwoFactor: true });
    expect(store.user()).toBeNull();
  });

  it('sends a recovery code only to the sign-in endpoint and accepts the completed session', async () => {
    const pending = store.login(
      'learner@example.test',
      'Long learner password 42!',
      undefined,
      'abcde-fghij',
    );
    await csrf();
    const request = http.expectOne('/api/v1/auth/login');
    expect(request.request.body.recoveryCode).toBe('abcde-fghij');
    expect(request.request.body.authenticatorCode).toBeUndefined();
    request.flush(session);
    await pending;
    expect(store.user()?.id).toBe(session.user.id);
  });

  it('keeps the visible session when server logout fails, then clears it on success', async () => {
    const login = store.login('learner@example.test', 'Long learner password 42!');
    await csrf();
    http.expectOne('/api/v1/auth/login').flush(session);
    await login;
    const failed = store.logout().catch(() => undefined);
    await csrf();
    http.expectOne('/api/v1/auth/logout').flush({}, { status: 503, statusText: 'Unavailable' });
    await failed;
    expect(store.user()?.id).toBe(session.user.id);
    const success = store.logout();
    await csrf();
    http.expectOne('/api/v1/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
    await success;
    expect(store.user()).toBeNull();
  });
});
