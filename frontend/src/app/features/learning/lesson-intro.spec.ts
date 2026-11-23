import { TestBed } from '@angular/core/testing';
import { LessonIntro } from './lesson-intro';

describe('Teaching new words before questions', () => {
  it('shows each word and meaning before offering to start the questions', async () => {
    const fixture = TestBed.createComponent(LessonIntro);
    fixture.componentRef.setInput('courseId', '10000000-0000-0000-0000-000000000001');
    fixture.componentRef.setInput('explanation', 'Hola — Hello\nGracias — Thank you');
    const ready = vi.fn();
    fixture.componentInstance.ready.subscribe(ready);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Hola');
    expect(host.textContent).toContain('Hello');
    expect(host.textContent).not.toContain('Start the questions');
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((button) => button.textContent?.includes('Next word'))!
      .click();
    await fixture.whenStable();
    expect(host.textContent).toContain('Gracias');
    expect(host.textContent).toContain('Thank you');
    expect(ready).not.toHaveBeenCalled();
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((button) => button.textContent?.includes('Start the questions'))!
      .click();
    expect(ready).toHaveBeenCalledOnce();
  });
});
