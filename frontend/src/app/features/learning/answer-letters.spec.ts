import { TestBed } from '@angular/core/testing';
import { AnswerLetters } from './answer-letters';

describe('Language letters while writing an answer', () => {
  it('replaces the selected letter with an accent and preserves the cursor', async () => {
    const fixture = TestBed.createComponent(AnswerLetters);
    const field = document.createElement('textarea');
    field.value = 'dias';
    field.setSelectionRange(1, 2);
    fixture.componentRef.setInput('courseId', 'course-1');
    fixture.componentRef.setInput('field', field);
    fixture.componentRef.setInput('value', 'dias');
    fixture.componentInstance.value.subscribe((value) => (field.value = value));
    await fixture.whenStable();
    fixture.nativeElement.querySelector('[aria-label="Insert í"]').click();
    expect(fixture.componentInstance.value()).toBe('días');
    expect(field.selectionStart).toBe(2);
    expect(field.selectionEnd).toBe(2);
  });
  it('offers Cyrillic with uppercase letters and respects a disabled answer field', async () => {
    const fixture = TestBed.createComponent(AnswerLetters);
    const field = document.createElement('textarea');
    fixture.componentRef.setInput('courseId', 'course-4');
    fixture.componentRef.setInput('field', field);
    fixture.componentInstance.value.subscribe((value) => (field.value = value));
    await fixture.whenStable();
    fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();
    fixture.nativeElement.querySelector('[aria-label="Insert Я"]').click();
    expect(fixture.componentInstance.value()).toBe('Я');
    fixture.componentRef.setInput('disabled', true);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[aria-label="Insert Ё"]').disabled).toBe(true);
  });
});
