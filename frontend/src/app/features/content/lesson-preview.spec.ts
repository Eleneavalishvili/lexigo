import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import { ContentApi, StaffLesson } from '../../core/content/content-api';
import { MediaApi } from '../../core/content/media-api';
import { LessonPreview } from './lesson-preview';

describe('LessonPreview audio', () => {
  it('opens only requested audio and removes it when changing exercise', async () => {
    const preview = vi
      .fn()
      .mockResolvedValue({ url: 'https://example.test/private.wav', expiresInSeconds: 60 });
    TestBed.configureTestingModule({
      providers: [
        { provide: MediaApi, useValue: { preview } },
        { provide: ContentApi, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(LessonPreview);
    const exercise = {
      id: 'first',
      prompt: 'Listen and type',
      explanation: '',
      type: 'Listening',
      orderNumber: 1,
      audioMediaId: 'recording',
      options: [],
      answers: [],
    };
    fixture.componentRef.setInput('lesson', {
      id: 'lesson',
      title: 'Sample',
      versionId: 'version',
      versionStatus: 'Draft',
      versionNumber: 1,
      reviews: [],
      unitId: 'unit',
      ownerId: 'author',
      currentVersionId: null,
      versionCreatedBy: 'author',
      objective: '',
      explanation: '',
      exercises: [exercise, { ...exercise, id: 'second', audioMediaId: null }],
    } satisfies StaffLesson);
    fixture.detectChanges();
    const root: HTMLElement = fixture.nativeElement;
    const button = (name: string) =>
      [...root.querySelectorAll('button')].find((item) => item.textContent?.trim() === name)!;
    expect(preview).not.toHaveBeenCalled();
    button('Listen to exercise').click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(preview).toHaveBeenCalledWith('recording');
    expect(root.querySelector('audio')?.getAttribute('src')).toBe(
      'https://example.test/private.wav',
    );
    button('Next exercise').click();
    fixture.detectChanges();
    expect(root.querySelector('audio')).toBeNull();
  });
});
