import { teachingWord } from './word-teaching';

describe('One expression followed by its exercise', () => {
  const notes = 'Hola — Hello\nBuenos días — Good morning\nGracias — Thank you';

  it('finds the phrase being practised from its English meaning or target text', () => {
    expect(teachingWord(notes, 'Translate into Spanish: Good morning.')).toEqual({
      target: 'Buenos días',
      meaning: 'Good morning',
    });
    expect(teachingWord(notes, 'Translate into English: Gracias'))?.toEqual({
      target: 'Gracias',
      meaning: 'Thank you',
    });
  });

  it('does not mistake part of an unrelated word for a teaching item', () => {
    expect(teachingWord('one — uno', 'Translate someone')).toBeNull();
  });

  it('matches Cyrillic and chooses the longest expression', () => {
    expect(
      teachingWord(
        'дом — house\nЭто мой дом. — This is my house.',
        'Translate into English: Это мой дом.',
      ),
    )?.toEqual({ target: 'Это мой дом.', meaning: 'This is my house.' });
  });
  it.each([
    ['Hoy llueve. Leo un libro en casa.', 'It is raining today. I am reading a book at home.'],
    ['Oggi piove. Leggo un libro a casa.', 'It is raining today. I am reading a book at home.'],
    [
      'Heute regnet es. Ich lese zu Hause ein Buch.',
      'It is raining today. I am reading a book at home.',
    ],
    [
      'Сегодня идёт дождь. Я читаю книгу дома.',
      'It is raining today. I am reading a book at home.',
    ],
  ])(
    'teaches a reading passage even when the English question contains no vocabulary match: %s',
    (target, meaning) => {
      const notes = `New words\n\nRead it in context\n${target}\n${meaning}`;
      expect(teachingWord(notes, 'What is the weather like?', target)).toEqual({ target, meaning });
    },
  );
  it('recognizes phrase sections as written teaching material', () => {
    expect(
      teachingWord(
        'A useful phrase\nLeo un libro.\nI am reading a book.',
        'Choose the phrase meaning: I am reading a book.',
      ),
    ).toEqual({ target: 'Leo un libro.', meaning: 'I am reading a book.' });
  });
});
