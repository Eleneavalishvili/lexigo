export interface BasicsSection {
  title: string;
  explanation: string;
  examples: readonly [string, string][];
}

export interface LanguageBasicsGuide {
  name: string;
  courseId: string;
  sections: readonly BasicsSection[];
}

export const LANGUAGE_BASICS: Readonly<Record<string, LanguageBasicsGuide>> = {
  es: {
    name: 'Spanish',
    courseId: '10000000-0000-0000-0000-000000000001',
    sections: [
      {
        title: 'Articles and gender',
        explanation:
          'Learn a noun with its article. El and un accompany masculine singular nouns; la and una accompany feminine singular nouns. Many nouns ending in -o are masculine and many ending in -a are feminine, but there are exceptions. Agua is feminine: it takes el directly before its stressed initial a in the singular, while its adjective stays feminine.',
        examples: [
          ['el niño / la niña', 'the boy / the girl'],
          ['un libro / una casa', 'a book / a house'],
          ['el agua fría', 'the cold water'],
        ],
      },
      {
        title: 'Useful verbs',
        explanation:
          'Ser identifies people and things; estar describes location and many states. A conjugated verb can identify its subject, so Spanish often leaves out yo. Regular -ar verbs change their ending: hablar becomes hablo with yo and hablas with tú.',
        examples: [
          ['Soy estudiante.', 'I am a student.'],
          ['La puerta está abierta.', 'The door is open.'],
          ['Hablo español.', 'I speak Spanish.'],
        ],
      },
      {
        title: 'Accents, ñ and questions',
        explanation:
          'Ñ is a separate letter, not an accented n. A written accent can show stress or distinguish words. The h in hola is silent. Spanish questions and exclamations use an opening as well as a closing mark. Our forgiving practice can accept missing Latin accents, but the reference spelling keeps them.',
        examples: [
          ['niño', 'boy'],
          ['té / te', 'tea / you (object pronoun)'],
          ['¿Cómo te llamas?', 'What is your name?'],
        ],
      },
    ],
  },
  it: {
    name: 'Italian',
    courseId: '10000000-0000-0000-0000-000000000002',
    sections: [
      {
        title: 'Articles and gender',
        explanation:
          'Learn the article together with the noun. Masculine singular nouns commonly use il; lo is used before sounds such as s plus a consonant and z. Feminine singular nouns use la. Before a vowel, the definite article becomes l’. A masculine indefinite article is un (or uno before the lo group); the feminine is una, shortened to un’ before a vowel.',
        examples: [
          ['il libro / lo studente', 'the book / the student'],
          ['la casa / l’acqua', 'the house / the water'],
          ['un uomo / un’amica', 'a man / a female friend'],
        ],
      },
      {
        title: 'Verbs and agreement',
        explanation:
          'Essere means to be: sono with io, sei with tu and è with lui or lei. Regular -are verbs use -o with io: parlare becomes parlo. Italian often omits the subject pronoun. An adjective commonly changes its ending to agree with the noun’s gender and number.',
        examples: [
          ['Sono studente.', 'I am a male student.'],
          ['Parlo italiano.', 'I speak Italian.'],
          ['La cucina è piccola.', 'The kitchen is small.'],
        ],
      },
      {
        title: 'Accents and everyday expressions',
        explanation:
          'È means is; e means and. Keep the final written accents in words such as caffè and città. Ciao is informal and can mean hello or goodbye; buongiorno is a polite daytime greeting. Ch before e or i has a hard k sound, as in chi.',
        examples: [
          ['un caffè', 'a coffee'],
          ['Ciao, come stai?', 'Hello, how are you? (informal)'],
          ['Mi chiamo Anna.', 'My name is Anna.'],
        ],
      },
    ],
  },
  de: {
    name: 'German',
    courseId: '10000000-0000-0000-0000-000000000003',
    sections: [
      {
        title: 'Articles and grammatical gender',
        explanation:
          'Learn nouns with der, die or das: masculine, feminine and neuter. Gender is grammatical; das Mädchen is neuter although it refers to a girl. All German nouns start with a capital letter. In the nominative, ein accompanies masculine and neuter nouns, and eine accompanies feminine nouns.',
        examples: [
          ['der Mann / die Frau / das Kind', 'the man / the woman / the child'],
          ['ein Junge / eine Frau / ein Mädchen', 'a boy / a woman / a girl'],
        ],
      },
      {
        title: 'Word order and verbs',
        explanation:
          'In a simple statement, the conjugated verb is the second sentence element, not necessarily the second word. If a time expression comes first, the subject follows the verb. In a yes/no question the conjugated verb comes first.',
        examples: [
          ['Ich lerne Deutsch.', 'I am learning German.'],
          ['Heute lerne ich Deutsch.', 'Today I am learning German.'],
          ['Lernst du Deutsch?', 'Are you learning German?'],
        ],
      },
      {
        title: 'A first look at cases',
        explanation:
          'The nominative commonly marks the subject; the accusative commonly marks the direct object. Masculine der changes to den and ein to einen in the accusative. The dative appears after mit and for many indirect objects; masculine/neuter definite articles become dem. Learn prepositions with the case they require.',
        examples: [
          ['Der Mann liest.', 'The man is reading.'],
          ['Ich sehe den Mann.', 'I see the man.'],
          ['Ich fahre mit dem Bus.', 'I travel by bus.'],
        ],
      },
      {
        title: 'Umlauts and ß',
        explanation:
          'Ä, ö and ü change a vowel’s sound. When those letters are unavailable, ae, oe and ue are conventional replacements. ß can be replaced by ss when necessary. Our forgiving practice also accepts omitted umlauts, but these letters can distinguish different words in normal German.',
        examples: [
          ['Tür / Tuer', 'door'],
          ['schön / schon', 'beautiful / already'],
          ['Straße / Strasse', 'street'],
        ],
      },
    ],
  },
  ru: {
    name: 'Russian',
    courseId: '10000000-0000-0000-0000-000000000004',
    sections: [
      {
        title: 'Read Cyrillic first',
        explanation:
          'Russian has 33 letters. Familiar-looking letters can have different sounds: Н is n, Р is r and С is s. Е and Ё are distinct letters. Ь and Ъ have no independent vowel or consonant sound. Open the complete alphabet below for letter names and example audio; word stress and surrounding letters affect pronunciation.',
        examples: [
          ['мама', 'mum / mother'],
          ['нет', 'no'],
          ['ёлка', 'fir tree'],
        ],
      },
      {
        title: 'Gender and the missing article',
        explanation:
          'Russian has no words corresponding directly to a and the. Many nouns ending in a consonant are masculine, -а or -я are often feminine, and -о or -е are often neuter. These are starting patterns, not absolute rules: папа is masculine, and soft-sign nouns need to be learned individually.',
        examples: [
          ['мой дом', 'my house (masculine)'],
          ['моя книга', 'my book (feminine)'],
          ['моё окно', 'my window (neuter)'],
        ],
      },
      {
        title: 'Present-tense verbs',
        explanation:
          'Russian usually leaves out present-tense is and am in simple statements. Other verbs are conjugated: читать becomes читаю with я and читаешь with ты. Keep the Cyrillic spelling when answering Russian questions; Latin transliteration is not treated as the same word.',
        examples: [
          ['Я студент.', 'I am a male student.'],
          ['Я читаю.', 'I am reading.'],
          ['Ты читаешь.', 'You are reading. (informal singular)'],
        ],
      },
      {
        title: 'A first look at cases',
        explanation:
          'A noun’s ending can change with its role in a sentence. In these examples книга is the subject/dictionary form, книгу is a direct object, школе is a location after в, and книги follows нет. These are examples of nominative, accusative, prepositional and genitive; Russian has other patterns and cases too.',
        examples: [
          ['Это книга.', 'This is a book.'],
          ['Я читаю книгу.', 'I am reading a book.'],
          ['Я в школе.', 'I am at school.'],
          ['У меня нет книги.', 'I do not have a book.'],
        ],
      },
    ],
  },
};
