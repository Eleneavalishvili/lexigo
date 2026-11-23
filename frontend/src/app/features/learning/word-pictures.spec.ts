import { hasWordPictures, wordPicture } from './word-pictures';

describe('Original word illustrations', () => {
  it.each(['té', 'tè', 'Tee', 'чай'])(
    'maps the same pictured meaning in each language: %s',
    (word) => {
      expect(wordPicture(word)?.src).toBe('/lesson-art/tea.svg');
    },
  );
  it('supports written articles without using arbitrary text as an image URL', () => {
    expect(wordPicture('ein Mädchen')?.src).toBe('/lesson-art/girl.svg');
    expect(wordPicture('un niño')?.src).toBe('/lesson-art/boy.svg');
    expect(wordPicture('../private.svg')).toBeNull();
    expect(wordPicture('Hola')).toBeNull();
    expect(
      hasWordPictures({
        id: 'question',
        type: 'MultipleChoice',
        prompt: 'Choose tea',
        options: [
          { id: 'one', text: 'Tee' },
          { id: 'two', text: 'unmapped' },
        ],
      }),
    ).toBe(false);
  });
});
