import { ExerciseView } from '../../core/learning/learning-api';

const nouns: Readonly<Record<string, readonly string[]>> = {
  coffee: ['coffee', 'café', 'caffè', 'kaffee', 'кофе'],
  milk: ['milk', 'leche', 'latte', 'milch', 'молоко'],
  tea: ['tea', 'té', 'tè', 'tee', 'чай'],
  water: ['water', 'agua', 'acqua', 'wasser', 'вода'],
  boy: ['boy', 'niño', 'bambino', 'junge', 'мальчик'],
  girl: ['girl', 'niña', 'bambina', 'mädchen', 'девочка'],
  man: ['man', 'hombre', 'uomo', 'mann', 'мужчина'],
  woman: ['woman', 'mujer', 'donna', 'frau', 'женщина'],
  bread: ['bread', 'pan', 'pane', 'brot', 'хлеб'],
  apple: ['apple', 'manzana', 'mela', 'apfel', 'яблоко'],
  sugar: ['sugar', 'azúcar', 'zucchero', 'zucker', 'сахар'],
  rice: ['rice', 'arroz', 'riso', 'reis', 'рис'],
};

export function wordPicture(text: string): { src: string; description: string } | null {
  const noun = text
    .normalize('NFC')
    .toLowerCase()
    .trim()
    .replace(/[.!?]+$/u, '')
    .replace(/^(the |a |an |el |la |un |una |il |lo |der |die |das |ein |eine |l['’])/u, '');
  const key = Object.keys(nouns).find((key) => nouns[key].includes(noun));
  return key ? { src: '/lesson-art/' + key + '.svg', description: 'Illustration: ' + key } : null;
}

export function hasWordPictures(exercise: ExerciseView): boolean {
  return (
    exercise.type === 'MultipleChoice' &&
    exercise.options.length >= 2 &&
    exercise.options.every((option) => wordPicture(option.text) !== null)
  );
}
