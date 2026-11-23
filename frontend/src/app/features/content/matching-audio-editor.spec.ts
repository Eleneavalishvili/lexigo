import { TestBed } from '@angular/core/testing';
import { MatchingAudioEditor } from './matching-audio-editor';

describe('Matching recording rows', () => {
  it('preserves each selected recording and explicitly removes it without exposing IDs', async () => {
    TestBed.configureTestingModule({ imports: [MatchingAudioEditor] });
    const fixture = TestBed.createComponent(MatchingAudioEditor);
    fixture.componentRef.setInput('labels', ['Clip one', 'Clip two']);
    fixture.componentRef.setInput('languageId', 2);
    fixture.componentRef.setInput('ids', ['private-media-one', null]);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Recording selected');
    expect(host.textContent).not.toContain('private-media-one');
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((button) => button.textContent?.includes('Use text only'))!
      .click();
    expect(fixture.componentInstance.ids()).toEqual([null, null]);
  });

  it('drops references for removed rows instead of attaching them to an absent item', async () => {
    TestBed.configureTestingModule({ imports: [MatchingAudioEditor] });
    const fixture = TestBed.createComponent(MatchingAudioEditor);
    fixture.componentRef.setInput('labels', ['One', 'Two']);
    fixture.componentRef.setInput('languageId', 2);
    fixture.componentRef.setInput('ids', ['first', 'second']);
    await fixture.whenStable();
    fixture.componentRef.setInput('labels', ['One']);
    await fixture.whenStable();
    expect(fixture.componentInstance.ids()).toEqual(['first']);
  });
});
