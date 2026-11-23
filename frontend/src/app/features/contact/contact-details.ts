// Publish only confirmed contact details. Never invent an inbox or legal entity.
export const contactDetails: {
  brand: string;
  supportEmail: string | null;
  legalName: string | null;
  address: string | null;
  addressIsDemo: boolean;
  supportHours: string | null;
} = {
  brand: 'LexiGo',
  supportEmail: 'lexigolanguage@gmail.com',
  legalName: 'LexiGoLanguage LLC',
  address: '12 Curiosity Lane, Tbilisi, Georgia',
  addressIsDemo: true,
  supportHours: 'Monday–Friday · 10:00–18:00 · Tbilisi time (UTC+4)',
};
