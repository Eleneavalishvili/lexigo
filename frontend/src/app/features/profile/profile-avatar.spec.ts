import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../../core/accounts/auth-store';
import { ProfileAvatar } from './profile-avatar';

describe('Profile photo', () => {
  it('shows the owner’s photo and removes it only after the server confirms', async () => {
    TestBed.configureTestingModule({
      imports: [ProfileAvatar],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthStore, useValue: { user: signal({ id: 'learner' }) } },
      ],
    });
    const fixture = TestBed.createComponent(ProfileAvatar);
    const http = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
    http
      .expectOne({ method: 'GET', url: '/api/v1/me/avatar' })
      .flush({ url: 'https://example.test/avatar' });
    await fixture.whenStable();
    fixture.detectChanges();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('img')).not.toBeNull();
    host.querySelector('button')!.click();
    const remove = http.expectOne({ method: 'DELETE', url: '/api/v1/me/avatar' });
    expect(host.querySelector('img')).not.toBeNull();
    remove.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(host.querySelector('img')).toBeNull();
    expect(host.textContent).toContain('Profile photo removed.');
    http.verify();
  });
});
