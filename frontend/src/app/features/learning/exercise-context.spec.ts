import { TestBed } from '@angular/core/testing';
import { ExerciseContext } from './exercise-context';

describe('Reading and dialogue context', () => {
  it('renders the target-language text without injecting HTML and labels its purpose', async () => {
    TestBed.configureTestingModule({ imports: [ExerciseContext] });
    const fixture = TestBed.createComponent(ExerciseContext);
    fixture.componentRef.setInput('text', 'Primera línea.\n<img src=x onerror="bad()">');
    fixture.componentRef.setInput('kind', 'Reading');
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('section')!.getAttribute('aria-label')).toBe('Reading passage');
    expect(host.querySelector('.context-text')!.textContent).toContain('<img src=x');
    expect(host.querySelector('img')).toBeNull();
    fixture.componentRef.setInput('kind', 'DialogueChoice');
    await fixture.whenStable();
    expect(host.querySelector('section')!.getAttribute('aria-label')).toBe('Dialogue situation');
    expect(host.textContent).toContain('You’re in the conversation.');
  });
});
