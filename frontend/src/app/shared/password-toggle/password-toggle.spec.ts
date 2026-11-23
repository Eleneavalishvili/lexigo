import { TestBed } from '@angular/core/testing';
import { PasswordToggle } from './password-toggle';

describe('PasswordToggle', () => {
  it('exposes the visibility state and its controlled input to assistive technology', async () => {
    const fixture = TestBed.createComponent(PasswordToggle);
    fixture.componentRef.setInput('inputId', 'test-password');
    await fixture.whenStable();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(button.type).toBe('button');
    expect(button.getAttribute('aria-controls')).toBe('test-password');
    expect(button.getAttribute('aria-label')).toBe('Show password');
    button.click();
    await fixture.whenStable();
    expect(fixture.componentInstance.visible()).toBe(true);
    expect(button.getAttribute('aria-label')).toBe('Hide password');
    expect(button.getAttribute('aria-pressed')).toBe('true');
  });
});
