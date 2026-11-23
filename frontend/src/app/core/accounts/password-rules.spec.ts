import { FormControl } from '@angular/forms';
import { newPasswordValidators } from './password-rules';

describe('new password rules', () => {
  it.each([
    'lowercase password!',
    'Uppercase without punctuation',
    'UPPERCASE123456    ',
    'Short!',
    'A!'.repeat(65),
  ])('rejects a password missing a requirement: %s', (password) => {
    expect(new FormControl(password, newPasswordValidators).valid).toBe(false);
  });

  it.each(['A memorable password!', 'Another long password 🦋'])(
    'accepts uppercase and a symbol: %s',
    (password) => {
      expect(new FormControl(password, newPasswordValidators).valid).toBe(true);
    },
  );
});
