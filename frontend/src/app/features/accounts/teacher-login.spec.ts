import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { TeacherLogin } from './teacher-login';

describe('Teacher code sign-in', () => {
  const teacherLogin = vi.fn();
  beforeEach(() => {
    teacherLogin.mockReset();
    TestBed.configureTestingModule({
      imports: [TeacherLogin],
      providers: [provideRouter([]), { provide: AuthStore, useValue: { teacherLogin } }],
    });
  });
  async function open() {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(TeacherLogin);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    for (const [id, value] of [
      ['teacher-access', '123456'],
      ['teacher-authenticator', '654321'],
    ]) {
      const field = host.querySelector<HTMLInputElement>('#' + id)!;
      field.value = value;
      field.dispatchEvent(new Event('input'));
    }
    return { fixture, host, navigate };
  }
  it('opens the teacher desk only after the code sign-in succeeds', async () => {
    teacherLogin.mockResolvedValue(undefined);
    const { fixture, host, navigate } = await open();
    host.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();
    expect(teacherLogin).toHaveBeenCalledExactlyOnceWith('123456', '654321');
    expect(navigate).toHaveBeenCalledExactlyOnceWith('/classes');
  });
  it('keeps rejected codes on the teacher form instead of redirecting to learner sign-in', async () => {
    teacherLogin.mockRejectedValue(new HttpErrorResponse({ status: 401 }));
    const { fixture, host, navigate } = await open();
    host.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();
    expect(navigate).not.toHaveBeenCalled();
    expect(host.querySelector('[role=alert]')?.textContent).toContain('same teacher');
  });
});
