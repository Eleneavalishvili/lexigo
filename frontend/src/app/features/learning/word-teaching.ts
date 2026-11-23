export interface TeachingWord {
  target: string;
  meaning: string;
}

function normalize(text: string): string {
  return text
    .normalize('NFC')
    .toLowerCase()
    .replace(/[^\p{L}\p{N}]+/gu, ' ')
    .trim();
}

export function teachingWords(explanation: string): TeachingWord[] {
  const lines = explanation.split('\n');
  const words = lines
    .filter((line) => line.includes(' — '))
    .map((line) => {
      const [target, ...meaning] = line.split(' — ');
      return { target: target.trim(), meaning: meaning.join(' — ').trim() };
    });
  for (let index = 0; index < lines.length - 2; index++) {
    if (['A useful phrase', 'Read it in context'].includes(lines[index].trim())) {
      const target = lines[index + 1].trim();
      const meaning = lines[index + 2].trim();
      if (target && meaning) words.push({ target, meaning });
    }
  }
  return words.filter(
    (word, index) => words.findIndex((other) => other.target === word.target) === index,
  );
}

export function teachingWord(
  explanation: string,
  prompt: string,
  context?: string | null,
): TeachingWord | null {
  const candidates = teachingWords(explanation);
  const passage = context
    ? candidates.find((word) => normalize(word.target) === normalize(context))
    : undefined;
  if (passage) return passage;
  const question = ' ' + normalize(prompt) + ' ';
  // Match only public teaching notes, never a private exercise answer key.
  return (
    candidates
      .map((word) => ({
        word,
        length: Math.max(
          ...[word.target, word.meaning].map((part) => {
            const key = normalize(part);
            return key && question.includes(' ' + key + ' ') ? key.length : 0;
          }),
        ),
      }))
      .filter((match) => match.length > 0)
      .sort((left, right) => right.length - left.length)[0]?.word ?? null
  );
}
