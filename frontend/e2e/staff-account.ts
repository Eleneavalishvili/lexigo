import { execFileSync } from 'node:child_process';
import { createHmac } from 'node:crypto';
import { expect, type APIRequestContext, type Page } from '@playwright/test';
import { enterCredentials, registerLearner } from './test-account';

type StaffRole = 'Teacher' | 'ContentAuthor' | 'Reviewer' | 'Moderator' | 'Admin' | 'SuperAdmin';

function authenticatorCode(secret: string): string {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  const bits = [...secret.replace(/\s/g, '').toUpperCase()]
    .map((letter) => alphabet.indexOf(letter).toString(2).padStart(5, '0'))
    .join('');
  const key = Buffer.from((bits.match(/.{8}/g) ?? []).map((byte) => Number.parseInt(byte, 2)));
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30_000)));
  const hash = createHmac('sha1', key).update(counter).digest();
  const number = hash.readUInt32BE(hash[hash.length - 1] & 15) & 0x7fffffff;
  return String(number % 1_000_000).padStart(6, '0');
}

export async function signInStaff(
  page: Page,
  request: APIRequestContext,
  baseURL: string,
  inboxPort: string,
  database: string,
  roles: readonly StaffRole[],
  displayName?: string,
): Promise<string> {
  if (!/^lexigo-browser-db-[a-f0-9]{32}$/.test(database)) {
    throw new Error('Staff fixtures require the disposable browser database.');
  }
  const { email, password } = await registerLearner(page, request, baseURL, inboxPort, displayName);
  await page.goto('/account/security');
  await page.getByLabel('Your current password').fill(password);
  await page.getByRole('button', { name: /Set up my authenticator/ }).click();
  const key = await page.locator('code.shared-key').innerText();
  await page.getByLabel('Authenticator code').fill(authenticatorCode(key));
  await page.getByRole('button', { name: /Verify and turn on/ }).click();
  await expect(page.getByText(/All devices have been signed out/)).toBeVisible();
  const recoveryCode = await page.locator('.recovery-codes code').first().innerText();

  // The fixture assigns roles only. Registration, MFA enrollment and login use normal UI flows.
  const result = execFileSync(
    'docker',
    [
      'exec',
      '-i',
      database,
      'psql',
      '-XAtq',
      '-v',
      'ON_ERROR_STOP=1',
      '-U',
      'postgres',
      '-d',
      'postgres',
    ],
    {
      encoding: 'utf8',
      input: `
        BEGIN;
        WITH granted AS (
          INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
          SELECT u."Id", r."Id" FROM "Users" u CROSS JOIN "AspNetRoles" r
          WHERE u."Email" = '${email}' AND r."Name" IN (${roles.map((role) => `'${role}'`).join(',')})
          RETURNING 1
        ) SELECT count(*) FROM granted;
        INSERT INTO "UserLanguageScopes" ("UserId", "LanguageId")
        SELECT u."Id", l."Id" FROM "Users" u CROSS JOIN "Languages" l
        WHERE u."Email" = '${email}' AND l."Code" = 'es';
        COMMIT;
      `,
    },
  );
  expect(Number(result.trim())).toBe(roles.length);

  await page.getByRole('link', { name: /I’m ready to sign in/ }).click();
  await enterCredentials(page, email, password);
  await page.getByRole('button', { name: /Use a recovery code instead/ }).click();
  await page.getByLabel('Recovery code').fill(recoveryCode);
  await page.getByRole('button', { name: /Verify and sign in/ }).click();
  await expect(page).not.toHaveURL(/\/login(?:\?|$)/);
  return password;
}
