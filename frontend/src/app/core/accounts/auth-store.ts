import { HttpBackend, HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthResponse, CurrentAccount, TwoFactorResponse } from './auth-models';

@Injectable({ providedIn: 'root' })
export class AuthStore {
  // Auth requests bypass the bearer interceptor: CSRF tokens bind to the anonymous cookie flow.
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly router = inject(Router);
  private readonly account = signal<CurrentAccount | null>(null);
  readonly user = this.account.asReadonly();
  private access: string | null = null;
  private expires = 0;
  private pendingRefresh: Promise<AuthResponse | null> | null = null;
  private initialized = false;
  private readonly channel =
    typeof window !== 'undefined' && 'BroadcastChannel' in window
      ? new window.BroadcastChannel('lexigo-account')
      : null;

  constructor() {
    this.channel?.addEventListener('message', (event: MessageEvent<unknown>) => {
      if (event.data !== 'signed-out' && event.data !== 'account-changed') return;
      this.clearLocal();
      if (this.router.url.startsWith('/account')) void this.router.navigate(['/login']);
    });
    inject(DestroyRef).onDestroy(() => this.channel?.close());
  }

  async restore(): Promise<void> {
    if (this.initialized) return;
    try {
      await this.refresh();
    } catch {
      /* The sign-in page handles unavailable servers; no session is invented offline. */
    } finally {
      this.initialized = true;
    }
  }

  async login(
    email: string,
    password: string,
    authenticatorCode?: string,
    recoveryCode?: string,
  ): Promise<AuthResponse | TwoFactorResponse> {
    return this.lock(async () => {
      const response = await this.authPost<AuthResponse | TwoFactorResponse>('/login', {
        email,
        password,
        authenticatorCode,
        recoveryCode,
      });
      if ('accessToken' in response) {
        this.accept(response);
        this.channel?.postMessage('account-changed');
      }
      return response;
    });
  }

  refresh(): Promise<AuthResponse | null> {
    if (this.pendingRefresh) return this.pendingRefresh;
    this.pendingRefresh = this.lock(async () => {
      try {
        const response = await this.authPost<AuthResponse>('/refresh', {});
        this.accept(response);
        return response;
      } catch (error) {
        if (error instanceof HttpErrorResponse && error.status === 401) {
          this.clearLocal();
          return null;
        }
        throw error;
      }
    }).finally(() => {
      this.pendingRefresh = null;
    });
    return this.pendingRefresh;
  }

  async token(): Promise<string | null> {
    if (this.access && this.expires > Date.now() + 15_000) return this.access;
    if (!this.access && this.initialized && !this.user()) return null;
    return (await this.refresh())?.accessToken ?? null;
  }

  async logout(): Promise<void> {
    await this.lock(() => this.authPost<void>('/logout', {}));
    this.credentialsChanged();
  }

  credentialsChanged(): void {
    this.clearLocal();
    this.channel?.postMessage('signed-out');
  }

  private accept(response: AuthResponse): void {
    this.access = response.accessToken;
    this.expires = Date.parse(response.expiresAt);
    this.account.set(response.user);
    this.initialized = true;
  }

  private clearLocal(): void {
    this.access = null;
    this.expires = 0;
    this.account.set(null);
  }

  private async authPost<T>(path: string, body: unknown): Promise<T> {
    const setup = await firstValueFrom(
      this.http.get<{ requestToken: string }>('/api/v1/auth/csrf'),
    );
    return firstValueFrom(
      this.http.post<T>('/api/v1/auth' + path, body, {
        headers: new HttpHeaders({ 'X-XSRF-TOKEN': setup.requestToken }),
      }),
    );
  }

  private async lock<T>(operation: () => Promise<T>): Promise<T> {
    // Serialize rotations across tabs as well as within this store.
    return typeof navigator !== 'undefined' && navigator.locks
      ? await navigator.locks.request('lexigo-session', operation)
      : operation();
  }
}
