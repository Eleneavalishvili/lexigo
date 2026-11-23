import { Validators } from '@angular/forms';

export const newPasswordValidators = [
  Validators.required,
  Validators.minLength(12),
  Validators.maxLength(128),
  Validators.pattern(/[A-Z]/),
  Validators.pattern(/[\p{P}\p{S}]/u),
];

export const passwordGuidance =
  'Use 12–128 characters, including an uppercase letter (A–Z) and a symbol, such as !, @ or #.';
