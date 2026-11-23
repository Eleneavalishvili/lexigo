import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Pronunciation } from '../../core/learning/pronunciation';
import { CharacterCue, courseCompanion } from './character-cue';

describe('Human companion reactions', () => {
  it('keeps a named accessible character and paused state across reactions', async () => {
    TestBed.configureTestingModule({ imports: [CharacterCue] });
    const fixture = TestBed.createComponent(CharacterCue);
    await fixture.whenStable();
    const svg = fixture.nativeElement.querySelector('svg') as SVGElement;
    expect(svg.getAttribute('aria-label')).toContain('Mira');
    fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();
    expect(fixture.nativeElement.getAttribute('data-paused')).toBe('true');
    expect(fixture.nativeElement.querySelector('button').getAttribute('aria-pressed')).toBe('true');
    fixture.componentRef.setInput('mood', 'encouragement');
    await fixture.whenStable();
    expect(svg.getAttribute('aria-label')).toContain('offering encouragement');
    for (const character of ['nico', 'zara']) {
      fixture.componentRef.setInput('character', character);
      fixture.componentRef.setInput('mood', 'celebration');
      await fixture.whenStable();
      expect(svg.getAttribute('data-character')).toBe(character);
      expect(svg.getAttribute('data-mood')).toBe('celebration');
    }
    for (const mood of ['listening', 'checking', 'speaking']) {
      fixture.componentRef.setInput('mood', mood);
      await fixture.whenStable();
      expect(svg.getAttribute('data-mood')).toBe(mood);
    }
    expect(fixture.nativeElement.querySelector('.talking-mouth')).not.toBeNull();
  });

  it('follows actual playback including English prompts, then restores the answer reaction', async () => {
    const voice = { speaking: signal(false), language: signal('en') };
    TestBed.configureTestingModule({
      imports: [CharacterCue],
      providers: [{ provide: Pronunciation, useValue: voice }],
    });
    const fixture = TestBed.createComponent(CharacterCue);
    fixture.componentRef.setInput('character', 'nico');
    fixture.componentRef.setInput('mood', 'encouragement');
    await fixture.whenStable();
    voice.speaking.set(true);
    await fixture.whenStable();
    const svg = fixture.nativeElement.querySelector('svg');
    expect(svg.getAttribute('data-mood')).toBe('speaking');
    voice.speaking.set(false);
    await fixture.whenStable();
    expect(svg.getAttribute('data-mood')).toBe('encouragement');
    fixture.componentRef.setInput('audioLanguage', 'it');
    voice.speaking.set(true);
    await fixture.whenStable();
    expect(svg.getAttribute('data-mood')).toBe('encouragement');
    voice.language.set('it');
    await fixture.whenStable();
    expect(svg.getAttribute('data-mood')).toBe('speaking');
  });

  it('uses all three companions across the four courses', () => {
    expect(courseCompanion('10000000-0000-0000-0000-000000000001')).toBe('mira');
    expect(courseCompanion('10000000-0000-0000-0000-000000000002')).toBe('nico');
    expect(courseCompanion('10000000-0000-0000-0000-000000000003')).toBe('mira');
    expect(courseCompanion('10000000-0000-0000-0000-000000000004')).toBe('zara');
  });
});
