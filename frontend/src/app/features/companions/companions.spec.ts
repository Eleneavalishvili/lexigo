import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Companions } from './companions';

describe('Meet the companions', () => {
  it('changes all three companions without claiming a real AI conversation', async () => {
    TestBed.configureTestingModule({ imports: [Companions], providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(Companions);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    Array.from(host.querySelectorAll<HTMLButtonElement>('.reaction-buttons button'))
      .find((button) => button.textContent?.includes('Celebrate'))!
      .click();
    await fixture.whenStable();
    expect(host.querySelectorAll('svg[data-mood="celebration"]').length).toBe(3);
    expect(host.textContent).toContain('Mira.');
    expect(host.textContent).toContain('Nico.');
    expect(host.textContent).toContain('Zara.');
    expect(
      host.querySelector<HTMLButtonElement>('.reaction-buttons button[aria-pressed="true"]')!
        .textContent,
    ).toContain('Celebrate');
    expect(host.querySelector('iframe, video')).toBeNull();
  });
});
