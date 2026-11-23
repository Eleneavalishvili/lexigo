import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { CurrentAccount } from '../../core/accounts/auth-models';
import { HomeHero } from './home-hero';

describe('Home page account actions', () => {
  it('changes guest actions to learning actions when the session changes', async () => {
    const user = signal<CurrentAccount | null>(null);
    TestBed.configureTestingModule({
      imports: [HomeHero],
      providers: [provideRouter([]), { provide: AuthStore, useValue: { user } }],
    });
    const fixture = TestBed.createComponent(HomeHero);
    await fixture.whenStable();
    const actions = () => fixture.nativeElement.querySelector('.hero-actions') as HTMLElement;
    expect(actions().textContent).toContain('I already have an account');
    expect(actions().querySelector('a[href="/learn"]')).toBeNull();

    user.set({
      id: 'learner',
      email: 'learner@example.test',
      displayName: 'Learner',
      emailConfirmed: true,
      roles: ['Learner'],
    });
    await fixture.whenStable();
    expect(actions().textContent).not.toContain('I already have an account');
    expect(actions().querySelector('a[href="/login"]')).toBeNull();
    expect(actions().querySelector('a[href="/learn"]')?.textContent).toContain('Continue learning');

    user.set(null);
    await fixture.whenStable();
    expect(actions().textContent).toContain('I already have an account');
    expect(actions().querySelector('a[href="/learn"]')).toBeNull();
  });
});
