import { TestBed } from '@angular/core/testing';
import { SentenceBuilder } from './sentence-builder';

describe('Sentence word tiles', () => {
  async function open() {
    TestBed.configureTestingModule({ imports: [SentenceBuilder] });
    const fixture = TestBed.createComponent(SentenceBuilder);
    fixture.componentRef.setInput('options', [
      { id: 'one', text: 'very' },
      { id: 'two', text: 'very' },
      { id: 'three', text: 'good' },
    ]);
    await fixture.whenStable();
    return fixture;
  }
  it('keeps repeated words as separate tiles and allows removing/reordering without dragging', async () => {
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    const buttons = host.querySelectorAll<HTMLButtonElement>('.word-bank button');
    buttons[0].click();
    buttons[1].click();
    buttons[2].click();
    await fixture.whenStable();
    expect(fixture.componentInstance.order()).toEqual(['one', 'two', 'three']);
    expect(host.querySelector('.sentence-readout')?.textContent).toContain('very very good');
    host.querySelectorAll<HTMLButtonElement>('.tile-controls button')[3].click();
    await fixture.whenStable();
    expect(fixture.componentInstance.order()).toEqual(['one', 'three', 'two']);
    host.querySelector<HTMLButtonElement>('.selected-tiles .word-tile')!.click();
    await fixture.whenStable();
    expect(fixture.componentInstance.order()).toEqual(['three', 'two']);
    expect(host.querySelector<HTMLButtonElement>('.word-bank button')!.disabled).toBe(false);
  });
  it('prevents changes while an answer is being saved and clears a sentence explicitly', async () => {
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    host.querySelector<HTMLButtonElement>('.word-bank button')!.click();
    await fixture.whenStable();
    fixture.componentRef.setInput('disabled', true);
    await fixture.whenStable();
    expect(
      Array.from(host.querySelectorAll<HTMLButtonElement>('button')).every(
        (button) => button.disabled,
      ),
    ).toBe(true);
    fixture.componentRef.setInput('disabled', false);
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('.clear-tiles')!.click();
    await fixture.whenStable();
    expect(fixture.componentInstance.order()).toEqual([]);
  });
});
